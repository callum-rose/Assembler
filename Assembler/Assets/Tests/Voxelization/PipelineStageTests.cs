using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;
using Assembler.Voxelization;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxelization
{
	/// <summary>Scripted gateway: returns canned responses in order and records every call.</summary>
	internal sealed class FakeGateway : IAnthropicGateway
	{
		private readonly Queue<string> _responses = new();

		public List<(string Stage, string Model, IReadOnlyList<AnthropicMessage> Messages)> Calls { get; } = new();

		public FakeGateway Enqueue(string response)
		{
			_responses.Enqueue(response);
			return this;
		}

		public Task<string> SendAsync(
			string stage,
			string model,
			string systemPrompt,
			IReadOnlyList<AnthropicMessage> messages,
			CancellationToken ct,
			IReadOnlyList<AnthropicTool>? tools = null,
			Func<AnthropicToolUse, CancellationToken, Task<AnthropicToolResult>>? onToolUse = null,
			int maxToolIterations = AnthropicClient.DefaultMaxToolIterations)
		{
			Calls.Add((stage, model, messages.ToList()));
			if (_responses.Count == 0)
			{
				throw new InvalidOperationException("FakeGateway has no response queued.");
			}

			return Task.FromResult(_responses.Dequeue());
		}

		public void Dispose()
		{
		}
	}

	public sealed class PipelineStageTests
	{
		private const string PlanResponse = @"Here is the plan.
```vmodel
model: crate
version: 1
rigged: false
unit: 1
real_world_height: 99
origin: feet_center
palette:
  _: none
  W: ""#aa7733""
parts:
  - id: box
    parent: root
    pivot: [0, 0, 0]
    data: { encoding: planned, planned: layers, size: [2, 2, 1], offset: [-1, 0, 0], note: ""wooden crate"" }
poses:
```";

		private const string LayersResponse = "```layers\nWW\n\nWW\n```";

		private static readonly SetManifest Manifest = new()
		{
			Game = "test",
			Unit = 1f,
			Assets = new[]
			{
				new ManifestAsset { Id = "crate", RealWorldHeight = 2f },
			},
		};

		[Test]
		public void ManifestGenerator_ParsesFencedYaml()
		{
			var gateway = new FakeGateway().Enqueue("```yaml\ngame: g\nunit: 0.2\nassets:\n  - id: a\n    real_world_height: 1\n```");
			var manifest = new ManifestGenerator(gateway, VoxelizationConfig.Default)
				.GenerateAsync("a game", CancellationToken.None).GetAwaiter().GetResult();

			Assert.That(manifest.Game, Is.EqualTo("g"));
			Assert.That(manifest.Assets.Single().Id, Is.EqualTo("a"));
			Assert.That(gateway.Calls.Single().Stage, Is.EqualTo(ManifestGenerator.Stage));
		}

		[Test]
		public void ManifestGenerator_RetriesOnceOnUnparseableResponse()
		{
			var gateway = new FakeGateway()
				.Enqueue("no fence here")
				.Enqueue("```yaml\ngame: g\nunit: 0.2\nassets:\n  - id: a\n    real_world_height: 1\n```");

			var manifest = new ManifestGenerator(gateway, VoxelizationConfig.Default)
				.GenerateAsync("a game", CancellationToken.None).GetAwaiter().GetResult();

			Assert.That(manifest.Game, Is.EqualTo("g"));
			Assert.That(gateway.Calls.Count, Is.EqualTo(2));
		}

		[Test]
		public void ModelPlanner_ReanchorsScaleToManifest()
		{
			var gateway = new FakeGateway().Enqueue(PlanResponse);
			var plan = new ModelPlanner(gateway, VoxelizationConfig.Default)
				.PlanAsync(Manifest, Manifest.Assets[0], AnthropicImage.None, string.Empty, CancellationToken.None)
				.GetAwaiter().GetResult();

			// The plan said real_world_height: 99; the manifest owns scale.
			Assert.That(plan.Skeleton.Id, Is.EqualTo("crate"));
			Assert.That(plan.Skeleton.Unit, Is.EqualTo(1f));
			Assert.That(plan.Skeleton.RealWorldHeight, Is.EqualTo(2f));
			Assert.That(plan.Brief.IsEmpty, Is.True);
		}

		[Test]
		public void ModelPlanner_DemotesOverBudgetLayersPartsToScript()
		{
			var config = VoxelizationConfig.Default with { PartVoxelBudget = 3 };
			var gateway = new FakeGateway().Enqueue(PlanResponse);
			var plan = new ModelPlanner(gateway, config)
				.PlanAsync(Manifest, Manifest.Assets[0], AnthropicImage.None, string.Empty, CancellationToken.None)
				.GetAwaiter().GetResult();

			var planned = (PlannedPartData)plan.Skeleton.Parts.Single().Data;
			Assert.That(planned.PlannedEncoding, Is.EqualTo(PartEncoding.Script), "2x2x1 = 4 voxels > budget 3");
		}

		[Test]
		public void PartAuthor_ParsesLayersFence_AndRetriesWithFeedbackOnBadDimensions()
		{
			var planned = new PlannedPartData(PartEncoding.Layers, new Vector3Int(2, 2, 1), new Vector3Int(-1, 0, 0), "crate");
			var model = SkeletonModel(planned);
			var gateway = new FakeGateway()
				.Enqueue("```layers\nWWW\n\nWWW\n```") // 3 wide, declared 2 — invalid
				.Enqueue(LayersResponse);

			var data = (LayersPartData)new PartAuthor(gateway, VoxelizationConfig.Default)
				.AuthorAsync(model, ReferenceBrief.None, model.Parts[0], planned, string.Empty, CancellationToken.None)
				.GetAwaiter().GetResult();

			Assert.That(gateway.Calls.Count, Is.EqualTo(2));
			Assert.That(data.Layers, Is.EqualTo(new[] { "WW", "WW" }));

			// The retry message carried the validation error back.
			var retryMessages = gateway.Calls[1].Messages;
			Assert.That(retryMessages.Count, Is.EqualTo(3));
			Assert.That(retryMessages[2].Content, Does.Contain("size.x"));
		}

		[Test]
		public void SetOrchestrator_RunsPlanAuthorAssembleValidateExport()
		{
			var gateway = new FakeGateway().Enqueue(PlanResponse).Enqueue(LayersResponse);
			var orchestrator = new SetOrchestrator(
				gateway,
				VoxelizationConfig.Default,
				NullReferenceImageSource.Instance,
				StubScriptRunner.Failing("no scripts in this set"),
				new TokenUsageTracker());

			var result = orchestrator
				.RunAssetAsync(Manifest, Manifest.Assets[0], string.Empty, CancellationToken.None)
				.GetAwaiter().GetResult();

			Assert.That(result.Status, Is.EqualTo(ModelStatus.Ready),
				result.Error + "\n" + string.Join("\n", result.Report.Issues));
			Assert.That(result.Export, Is.Not.Null);
			Assert.That(result.Export!.Files.Keys, Does.Contain("crate.vmodel.yaml"));
			Assert.That(result.Export.Files.Keys, Does.Contain("crate.vox"));
			Assert.That(result.Assembled!.Composed.Voxels.Count, Is.EqualTo(4));
		}

		[Test]
		public void SetOrchestrator_FailedPlanBecomesFailedResult()
		{
			var gateway = new FakeGateway().Enqueue("nothing useful").Enqueue("still nothing");
			var orchestrator = new SetOrchestrator(
				gateway,
				VoxelizationConfig.Default,
				NullReferenceImageSource.Instance,
				StubScriptRunner.Failing("unused"),
				new TokenUsageTracker());

			var result = orchestrator
				.RunAssetAsync(Manifest, Manifest.Assets[0], string.Empty, CancellationToken.None)
				.GetAwaiter().GetResult();

			Assert.That(result.Status, Is.EqualTo(ModelStatus.Failed));
			Assert.That(result.Error, Does.Contain("vmodel"));
		}

		private static VoxelRigModel SkeletonModel(PlannedPartData planned) => new()
		{
			Id = "crate",
			Unit = 1f,
			RealWorldHeight = 2f,
			Palette = new[] { new PaletteEntry('W', new Color32(0xaa, 0x77, 0x33, 0xff)) },
			Parts = new[]
			{
				new VoxelPart { Id = "box", Pivot = Vector3Int.zero, Data = planned },
			},
		};
	}
}
