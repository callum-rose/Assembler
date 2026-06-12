using System.Linq;
using System.Threading;
using Assembler.Voxelization;
using NUnit.Framework;

namespace Tests.Voxelization
{
	/// <summary>
	/// The lightweight refine path (issue 307): an operator note becomes a
	/// targeted local edit applied to an already-generated model, re-authoring
	/// only the parts it names and never re-planning.
	/// </summary>
	public sealed class LocalEditorTests
	{
		private const string PlanResponse = @"```vmodel
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
			Assets = new[] { new ManifestAsset { Id = "crate", RealWorldHeight = 2f } },
		};

		private static (SetOrchestrator Orchestrator, FakeGateway Gateway, ModelResult Previous) Seed(params string[] refineResponses)
		{
			var gateway = new FakeGateway()
				.Enqueue(PlanResponse)
				.Enqueue(LayersResponse)
				.Enqueue("OK");
			foreach (var response in refineResponses)
			{
				gateway.Enqueue(response);
			}

			var orchestrator = new SetOrchestrator(
				gateway,
				VoxelizationConfig.Default,
				NullReferenceImageSource.Instance,
				StubScriptRunner.Failing("no scripts"),
				new TokenUsageTracker());

			var previous = orchestrator
				.RunAssetAsync(Manifest, Manifest.Assets[0], string.Empty, CancellationToken.None)
				.GetAwaiter().GetResult();
			Assert.That(previous.Status, Is.EqualTo(ModelStatus.Ready), previous.Error);
			return (orchestrator, gateway, previous);
		}

		[Test]
		public void Recolour_EditsThePaletteOnly_WithoutReauthoring()
		{
			var (orchestrator, gateway, previous) = Seed("```edit\npalette:\n  W: \"#112233\"\n```");

			var result = orchestrator
				.RefineAssetAsync(previous, "make the crate blue", CancellationToken.None)
				.GetAwaiter().GetResult();

			Assert.That(result.Status, Is.EqualTo(ModelStatus.Ready), result.Error);
			Assert.That(result.Model.Palette.Single().ToHex(), Is.EqualTo("#112233"));

			// Only the interpret call ran — no re-authoring for a pure recolour.
			Assert.That(gateway.Calls.Count, Is.EqualTo(4));
			Assert.That(gateway.Calls[3].Stage, Is.EqualTo(LocalEditor.Stage));
			Assert.That(result.Assembled!.Composed.Voxels.Count, Is.EqualTo(4), "geometry is untouched");
		}

		[Test]
		public void Move_ShiftsThePivot_WithoutReauthoring()
		{
			var (orchestrator, gateway, previous) = Seed("```edit\nparts:\n  box:\n    pivot: [1, 0, 0]\n```");

			var result = orchestrator
				.RefineAssetAsync(previous, "nudge the box right", CancellationToken.None)
				.GetAwaiter().GetResult();

			Assert.That(result.Status, Is.EqualTo(ModelStatus.Ready), result.Error);
			Assert.That(result.Model.FindPart("box")!.Pivot, Is.EqualTo(new UnityEngine.Vector3Int(1, 0, 0)));
			Assert.That(gateway.Calls.Count, Is.EqualTo(4), "interpret only — no re-author for a pure move");
		}

		[Test]
		public void Note_ReauthorsOnlyTheNamedPart()
		{
			var (orchestrator, gateway, previous) =
				Seed("```edit\nparts:\n  box:\n    note: \"add a lid detail\"\n```", LayersResponse);

			var result = orchestrator
				.RefineAssetAsync(previous, "give the crate a lid", CancellationToken.None)
				.GetAwaiter().GetResult();

			Assert.That(result.Status, Is.EqualTo(ModelStatus.Ready), result.Error);
			Assert.That(gateway.Calls.Count, Is.EqualTo(5));
			Assert.That(gateway.Calls[3].Stage, Is.EqualTo(LocalEditor.Stage));
			Assert.That(gateway.Calls[4].Stage, Is.EqualTo(PartAuthor.Stage));

			// The re-author call carried the operator's note as the part's feedback.
			Assert.That(gateway.Calls[4].Messages[0].Content, Does.Contain("add a lid detail"));
		}

		[Test]
		public void EmptyEdit_ReturnsThePreviousResultUnchanged()
		{
			var (orchestrator, gateway, previous) = Seed("```edit\n# needs a full redesign\n```");

			var result = orchestrator
				.RefineAssetAsync(previous, "turn it into a spaceship", CancellationToken.None)
				.GetAwaiter().GetResult();

			Assert.That(result, Is.SameAs(previous), "an un-applicable note is a no-op, not a failure");
			Assert.That(gateway.Calls.Count, Is.EqualTo(4), "interpret ran, nothing else");
		}

		[Test]
		public void RefineDoesNotReplan_SoItCannotFailThePlanGates()
		{
			// The whole point of issue 307: a refine never invokes the planner, so
			// the deterministic plan gates that reject re-plans can't error it out.
			var (orchestrator, gateway, previous) = Seed("```edit\npalette:\n  W: \"#445566\"\n```");

			orchestrator
				.RefineAssetAsync(previous, "darker wood", CancellationToken.None)
				.GetAwaiter().GetResult();

			Assert.That(gateway.Calls.Skip(3).Any(c => c.Stage == ModelPlanner.Stage), Is.False);
		}
	}
}
