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
target_height: 99
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
			Assets = new[]
			{
				new ManifestAsset { Id = "crate", Height = 2 },
			},
		};

		[Test]
		public void ManifestGenerator_ParsesFencedYaml()
		{
			var gateway = new FakeGateway().Enqueue("```yaml\ngame: g\nassets:\n  - id: a\n    height: 1\n```");
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
				.Enqueue("```yaml\ngame: g\nassets:\n  - id: a\n    height: 1\n```");

			var manifest = new ManifestGenerator(gateway, VoxelizationConfig.Default)
				.GenerateAsync("a game", CancellationToken.None).GetAwaiter().GetResult();

			Assert.That(manifest.Game, Is.EqualTo("g"));
			Assert.That(gateway.Calls.Count, Is.EqualTo(2));
		}

		[Test]
		public void RunFolderNamer_SlugifiesTheModelReplyToKebabCase()
		{
			var gateway = new FakeGateway().Enqueue("Pirate Cove Props");
			var slug = new RunFolderNamer(gateway, VoxelizationConfig.Default)
				.NameAsync(Manifest, CancellationToken.None).GetAwaiter().GetResult();

			Assert.That(slug, Is.EqualTo("pirate-cove-props"));
			Assert.That(gateway.Calls.Single().Stage, Is.EqualTo(RunFolderNamer.Stage));
		}

		[TestCase("medieval-village", "medieval-village")]
		[TestCase("  Neon Racers!! ", "neon-racers")]
		[TestCase("spaceships\nignored second line", "spaceships")]
		[TestCase("`under_water/scene`", "under-water-scene")]
		[TestCase("", "set")]
		[TestCase("***", "set")]
		public void RunFolderNamer_SlugifyIsFilesystemSafe(string raw, string expected) =>
			Assert.That(RunFolderNamer.Slugify(raw), Is.EqualTo(expected));

		[Test]
		public void ModelPlanner_ReanchorsScaleToManifest()
		{
			var gateway = new FakeGateway().Enqueue(PlanResponse);
			var plan = new ModelPlanner(gateway, VoxelizationConfig.Default)
				.PlanAsync(Manifest, Manifest.Assets[0], AnthropicImage.None, ReferenceBrief.None, string.Empty, CancellationToken.None)
				.GetAwaiter().GetResult();

			// The plan said target_height: 99; the manifest owns scale.
			Assert.That(plan.Skeleton.Id, Is.EqualTo("crate"));
			Assert.That(plan.Skeleton.TargetHeight, Is.EqualTo(2));
			Assert.That(plan.Brief.IsEmpty, Is.True);
		}

		[Test]
		public void ModelPlanner_DemotesOverBudgetLayersPartsToScript()
		{
			var config = VoxelizationConfig.Default with { PartVoxelBudget = 3 };
			var gateway = new FakeGateway().Enqueue(PlanResponse);
			var plan = new ModelPlanner(gateway, config)
				.PlanAsync(Manifest, Manifest.Assets[0], AnthropicImage.None, ReferenceBrief.None, string.Empty, CancellationToken.None)
				.GetAwaiter().GetResult();

			var planned = (PlannedPartData)plan.Skeleton.Parts.Single().Data;
			Assert.That(planned.PlannedEncoding, Is.EqualTo(PartEncoding.Script), "2x2x1 = 4 voxels > budget 3");
		}

		[Test]
		public void ModelPlanner_RejectsGeometricallyDoomedBilateralSkeleton_AndRetriesWithFeedback()
		{
			// The plan's only part is 2 wide at pivot x=0 — an even-width centre
			// part can never straddle the mirror plane. The planner must bounce it
			// back with the geometry error and accept the corrected odd-width plan.
			var manifest = new SetManifest
			{
				Game = "test",
				Assets = new[]
				{
					new ManifestAsset { Id = "crate", Height = 2, Symmetry = "bilateral" },
				},
			};
			var gateway = new FakeGateway()
				.Enqueue(PlanResponse)
				.Enqueue(PlanResponse.Replace("size: [2, 2, 1]", "size: [3, 2, 1]"));

			var plan = new ModelPlanner(gateway, VoxelizationConfig.Default)
				.PlanAsync(manifest, manifest.Assets[0], AnthropicImage.None, ReferenceBrief.None, string.Empty, CancellationToken.None)
				.GetAwaiter().GetResult();

			Assert.That(gateway.Calls.Count, Is.EqualTo(2));
			Assert.That(gateway.Calls[1].Messages[2].Content, Does.Contain("even width"));
			Assert.That(((PlannedPartData)plan.Skeleton.Parts.Single().Data).Size.x, Is.EqualTo(3));
		}

		[Test]
		public void ModelPlanner_RejectsAnInlineAuthoredPlan_AndRetriesWithFeedback()
		{
			// The planner emitted finished geometry (encoding: script) instead of a
			// `planned` placeholder — that skips authoring and assembles to nothing,
			// so it must bounce back and the corrected planned plan be accepted.
			const string inlineAuthored = @"```vmodel
model: crate
version: 1
rigged: false
target_height: 99
origin: feet_center
palette:
  _: none
  W: ""#aa7733""
parts:
  - id: box
    parent: root
    pivot: [0, 0, 0]
    data:
      encoding: script
      size: [2, 2, 1]
      offset: [-1, 0, 0]
      source: |-
        return b.Build();
poses:
```";
			var gateway = new FakeGateway().Enqueue(inlineAuthored).Enqueue(PlanResponse);

			var plan = new ModelPlanner(gateway, VoxelizationConfig.Default)
				.PlanAsync(Manifest, Manifest.Assets[0], AnthropicImage.None, ReferenceBrief.None, string.Empty, CancellationToken.None)
				.GetAwaiter().GetResult();

			Assert.That(gateway.Calls.Count, Is.EqualTo(2));
			Assert.That(gateway.Calls[1].Messages[2].Content, Does.Contain("SKELETON"));
			Assert.That(plan.Skeleton.Parts.Single().Data, Is.TypeOf<PlannedPartData>());
		}

		[Test]
		public void BriefExtractor_SymmetrizesTheSilhouetteForBilateralAssets()
		{
			var manifest = new SetManifest
			{
				Game = "test",
				Assets = new[]
				{
					new ManifestAsset { Id = "crate", Height = 2, Symmetry = "bilateral", Reference = "ref.png" },
				},
			};
			var gateway = new FakeGateway().Enqueue(@"```brief
reference_brief:
  source: ref.png
  silhouette:
    face: front
    size: [3, 2]
    rows:
      - ""#..""
      - ""#.#""
```");

			var brief = new BriefExtractor(gateway, VoxelizationConfig.Default)
				.ExtractAsync(manifest, manifest.Assets[0], new AnthropicImage("image/png", new byte[] { 1 }), CancellationToken.None)
				.GetAwaiter().GetResult();

			// Each row is unioned with its own reflection.
			Assert.That(brief.Silhouette.Rows, Is.EqualTo(new[] { "#.#", "#.#" }));
			Assert.That(gateway.Calls.Single().Stage, Is.EqualTo(BriefExtractor.Stage));
		}

		[Test]
		public void BriefExtractor_TrimsEmptyMarginFromTheSilhouette()
		{
			var manifest = new SetManifest
			{
				Game = "test",
				Assets = new[]
				{
					new ManifestAsset { Id = "crate", Height = 2, Reference = "ref.png" },
				},
			};
			var gateway = new FakeGateway().Enqueue(@"```brief
reference_brief:
  source: ref.png
  silhouette:
    face: front
    size: [5, 3]
    rows:
      - "".....""
      - "".###.""
      - "".#.#.""
```");

			var brief = new BriefExtractor(gateway, VoxelizationConfig.Default)
				.ExtractAsync(manifest, manifest.Assets[0], new AnthropicImage("image/png", new byte[] { 1 }), CancellationToken.None)
				.GetAwaiter().GetResult();

			Assert.That(brief.Silhouette.Rows, Is.EqualTo(new[] { "###", "#.#" }));
			Assert.That(brief.Silhouette.Size, Is.EqualTo(new Vector3Int(3, 2, 0)));
		}

		[Test]
		public void ModelPlanner_RejectsPaletteColoursOutsideTheBrief()
		{
			var brief = new ReferenceBrief
			{
				Source = "ref.png",
				Palette = new[] { new PaletteEntry('Z', new Color32(0x11, 0x22, 0x33, 0xff)) },
			};
			var gateway = new FakeGateway()
				.Enqueue(PlanResponse)
				.Enqueue(PlanResponse.Replace("#aa7733", "#112233"));

			var plan = new ModelPlanner(gateway, VoxelizationConfig.Default)
				.PlanAsync(Manifest, Manifest.Assets[0], AnthropicImage.None, brief, string.Empty, CancellationToken.None)
				.GetAwaiter().GetResult();

			Assert.That(gateway.Calls.Count, Is.EqualTo(2));
			Assert.That(gateway.Calls[1].Messages[2].Content, Does.Contain("locked reference palette"));
			Assert.That(plan.Skeleton.Palette.Single().ToHex(), Is.EqualTo("#112233"));
		}

		[Test]
		public void SetOrchestrator_ExtractsTheBriefBeforePlanning_WhenAReferenceImageExists()
		{
			var manifest = new SetManifest
			{
				Game = "test",
				Assets = new[]
				{
					new ManifestAsset { Id = "crate", Height = 2, Reference = "ref.png" },
				},
			};
			var images = new BytesReferenceImageSource(new Dictionary<string, AnthropicImage>
			{
				["ref.png"] = new("image/png", new byte[] { 1 }),
			});
			var gateway = new FakeGateway()
				.Enqueue(@"```brief
reference_brief:
  source: ref.png
  silhouette:
    face: front
    size: [2, 2]
    rows:
      - ""##""
      - ""##""
```")
				.Enqueue(PlanResponse)
				.Enqueue(LayersResponse)
				.Enqueue("OK");
			var orchestrator = new SetOrchestrator(
				gateway,
				VoxelizationConfig.Default,
				images,
				StubScriptRunner.Failing("no scripts"),
				new TokenUsageTracker());

			var result = orchestrator
				.RunAssetAsync(manifest, manifest.Assets[0], string.Empty, CancellationToken.None)
				.GetAwaiter().GetResult();

			Assert.That(result.Status, Is.EqualTo(ModelStatus.Ready),
				result.Error + "\n" + string.Join("\n", result.Report.Issues));
			Assert.That(gateway.Calls.Select(c => c.Stage).Take(2),
				Is.EqualTo(new[] { BriefExtractor.Stage, ModelPlanner.Stage }));

			// The planner received the transcribed silhouette as locked input.
			Assert.That(gateway.Calls[1].Messages[0].Content, Does.Contain("Reference brief (authoritative"));
			Assert.That(result.Brief.Silhouette.Rows, Is.EqualTo(new[] { "##", "##" }));
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
		public void PartAuthor_RegroupsRowsWhenBlankLinesAreMisplaced()
		{
			// Sonnet sometimes puts a blank line between every row (or none at
			// all). With 2 layers of 2 rows each, this response groups into 4
			// one-row layers — the author must re-chunk instead of failing.
			var planned = new PlannedPartData(PartEncoding.Layers, new Vector3Int(2, 2, 2), Vector3Int.zero, "block");
			var model = SkeletonModel(planned);
			var gateway = new FakeGateway().Enqueue("```layers\nWW\n\nW.\n\n.W\n\nWW\n```");

			var data = (LayersPartData)new PartAuthor(gateway, VoxelizationConfig.Default)
				.AuthorAsync(model, ReferenceBrief.None, model.Parts[0], planned, string.Empty, CancellationToken.None)
				.GetAwaiter().GetResult();

			Assert.That(gateway.Calls.Count, Is.EqualTo(1), "should succeed without a retry");
			Assert.That(data.Layers, Is.EqualTo(new[] { "WW\nW.", ".W\nWW" }));
		}

		[Test]
		public void PartUser_IncludesWorldExtent_AndBriefSilhouette()
		{
			var planned = new PlannedPartData(PartEncoding.Layers, new Vector3Int(2, 2, 1), new Vector3Int(-1, 0, 0), "crate");
			var model = SkeletonModel(planned);
			var brief = new ReferenceBrief
			{
				Source = "ref.png",
				Silhouette = new SilhouetteSpec("front", new Vector3Int(2, 2, 0), new[] { "##", ".#" }),
			};

			var prompt = VoxelizationPrompts.PartUser(model, brief, model.Parts[0], planned, string.Empty);

			Assert.That(prompt, Does.Contain("occupies world cells: x -1..0, y 0..1, z 0..0"));
			Assert.That(prompt, Does.Contain("front silhouette of the WHOLE model"));
			Assert.That(prompt, Does.Contain(".#"));
		}

		[Test]
		public void SetOrchestrator_ReauthorFeedbackShowsAsciiViewsOfWhatWasBuilt()
		{
			// One bilateral slab: the first authoring is lopsided (attributed
			// Asymmetric), the retry is symmetric. The re-author call must carry
			// ASCII views of the assembled model so the author can see the error.
			const string plan = @"```vmodel
model: slab
version: 1
rigged: false
target_height: 2
origin: feet_center
palette:
  _: none
  W: ""#aa7733""
parts:
  - id: slab
    parent: root
    pivot: [0, 0, 0]
    data: { encoding: planned, planned: layers, size: [3, 1, 2], offset: [-1, 0, -1], note: ""slab"" }
poses:
```";
			var manifest = new SetManifest
			{
				Game = "test",
				Assets = new[]
				{
					new ManifestAsset { Id = "slab", Height = 2, Symmetry = "bilateral" },
				},
			};
			var gateway = new FakeGateway()
				.Enqueue(plan)
				.Enqueue("```layers\nWWW\nWW.\n```")
				.Enqueue("```layers\nWWW\nW.W\n```")
				.Enqueue("OK");
			var orchestrator = new SetOrchestrator(
				gateway,
				VoxelizationConfig.Default,
				NullReferenceImageSource.Instance,
				StubScriptRunner.Failing("no scripts"),
				new TokenUsageTracker());

			var result = orchestrator
				.RunAssetAsync(manifest, manifest.Assets[0], string.Empty, CancellationToken.None)
				.GetAwaiter().GetResult();

			Assert.That(result.Status, Is.EqualTo(ModelStatus.Ready),
				result.Error + "\n" + string.Join("\n", result.Report.Issues));
			Assert.That(gateway.Calls.Count, Is.EqualTo(4), "plan, author, re-author, review");
			Assert.That(gateway.Calls[3].Stage, Is.EqualTo(SetOrchestrator.ReviewStage));

			var reauthor = gateway.Calls[2].Messages[0].Content;
			Assert.That(reauthor, Does.Contain("not left-right symmetric"));
			Assert.That(reauthor, Does.Contain("FRONT view"));
			Assert.That(reauthor, Does.Contain("WWW"));
		}

		[Test]
		public void SetOrchestrator_ReviewCorrectionsTriggerAFullReplanWithTheNote()
		{
			var gateway = new FakeGateway()
				.Enqueue(PlanResponse)
				.Enqueue(LayersResponse)
				.Enqueue("1. Make the crate one voxel wider at the base.") // review → corrections
				.Enqueue(PlanResponse)                                     // re-plan carries the note
				.Enqueue(LayersResponse);
			var orchestrator = new SetOrchestrator(
				gateway,
				VoxelizationConfig.Default,
				NullReferenceImageSource.Instance,
				StubScriptRunner.Failing("no scripts"),
				new TokenUsageTracker());

			var result = orchestrator
				.RunAssetAsync(Manifest, Manifest.Assets[0], string.Empty, CancellationToken.None)
				.GetAwaiter().GetResult();

			// MaxReviewRounds = 1: plan, author, review, re-plan, author — no second review.
			Assert.That(result.Status, Is.EqualTo(ModelStatus.Ready),
				result.Error + "\n" + string.Join("\n", result.Report.Issues));
			Assert.That(gateway.Calls.Count, Is.EqualTo(5));
			Assert.That(gateway.Calls[3].Messages[0].Content, Does.Contain("one voxel wider"));
		}

		[Test]
		public void SetOrchestrator_RunsPlanAuthorAssembleValidateExport()
		{
			var gateway = new FakeGateway().Enqueue(PlanResponse).Enqueue(LayersResponse).Enqueue("OK");
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
		public void SetOrchestrator_RefineAppliesEditsWithoutRePlanning()
		{
			var gateway = new FakeGateway().Enqueue(PlanResponse).Enqueue(LayersResponse).Enqueue("OK");
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
			var callsBeforeRefine = gateway.Calls.Count;
			var box = previous.Model.FindPart("box");

			// A pure recolour: no part is geometry-edited, so the only model call is
			// the refine itself — no brief, no plan, no authoring.
			gateway.Enqueue("```edits\n- { op: recolour, key: W, colour: \"#1188cc\" }\n```");
			var refined = orchestrator
				.RefineAssetAsync(Manifest, Manifest.Assets[0], previous, "make the crate blue", CancellationToken.None)
				.GetAwaiter().GetResult();

			Assert.That(refined.Status, Is.EqualTo(ModelStatus.Ready),
				refined.Error + "\n" + string.Join("\n", refined.Report.Issues));
			Assert.That(gateway.Calls.Count, Is.EqualTo(callsBeforeRefine + 1), "refine should make exactly one model call");
			Assert.That(gateway.Calls[callsBeforeRefine].Stage, Is.EqualTo(ModelRefiner.Stage));
			Assert.That(refined.Model.Palette.Single().ToHex(), Is.EqualTo("#1188cc"));

			// The untouched part is reference-equal, so its export is bit-identical.
			Assert.That(ReferenceEquals(refined.Model.FindPart("box"), box), Is.True);
		}

		[Test]
		public void SetOrchestrator_RefineEscalatesAReplanToTheFullPipeline()
		{
			var gateway = new FakeGateway().Enqueue(PlanResponse).Enqueue(LayersResponse).Enqueue("OK");
			var orchestrator = new SetOrchestrator(
				gateway,
				VoxelizationConfig.Default,
				NullReferenceImageSource.Instance,
				StubScriptRunner.Failing("no scripts"),
				new TokenUsageTracker());

			var previous = orchestrator
				.RunAssetAsync(Manifest, Manifest.Assets[0], string.Empty, CancellationToken.None)
				.GetAwaiter().GetResult();
			var callsBeforeRefine = gateway.Calls.Count;

			// A structural note: the refiner emits a single replan op, so the orchestrator
			// runs the full pipeline (refine call, then plan, author, review).
			gateway
				.Enqueue("```edits\n- { op: replan, reason: \"add a lid\" }\n```")
				.Enqueue(PlanResponse)
				.Enqueue(LayersResponse)
				.Enqueue("OK");

			var refined = orchestrator
				.RefineAssetAsync(Manifest, Manifest.Assets[0], previous, "add a lid", CancellationToken.None)
				.GetAwaiter().GetResult();

			Assert.That(refined.Status, Is.EqualTo(ModelStatus.Ready), refined.Error);
			Assert.That(gateway.Calls.Skip(callsBeforeRefine).Select(c => c.Stage),
				Is.EqualTo(new[] { ModelRefiner.Stage, ModelPlanner.Stage, PartAuthor.Stage, SetOrchestrator.ReviewStage }));

			// The re-plan was seeded with the previously accepted model.
			Assert.That(gateway.Calls[callsBeforeRefine + 1].Messages[0].Content,
				Does.Contain("previously accepted model"));
		}

		[Test]
		public void SetOrchestrator_FailedPlanBecomesFailedResult()
		{
			var gateway = new FakeGateway().Enqueue("nothing useful").Enqueue("still nothing").Enqueue("and again");
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
			TargetHeight = 2,
			Palette = new[] { new PaletteEntry('W', new Color32(0xaa, 0x77, 0x33, 0xff)) },
			Parts = new[]
			{
				new VoxelPart { Id = "box", Pivot = Vector3Int.zero, Data = planned },
			},
		};
	}
}
