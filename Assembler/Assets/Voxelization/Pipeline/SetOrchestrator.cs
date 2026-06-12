using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;
using UnityEngine;

namespace Assembler.Voxelization
{
	public enum ModelStatus
	{
		/// <summary>Assembled, validated clean, exported.</summary>
		Ready,

		/// <summary>Exported but with unresolved validation issues — needs a human look.</summary>
		NeedsReview,

		/// <summary>A stage threw; no export.</summary>
		Failed,
	}

	public sealed record ModelResult
	{
		public string AssetId { get; init; } = string.Empty;
		public ModelStatus Status { get; init; } = ModelStatus.Failed;
		public VoxelRigModel Model { get; init; } = new();
		public ReferenceBrief Brief { get; init; } = ReferenceBrief.None;
		public AssembledModel? Assembled { get; init; }
		public ValidationReport Report { get; init; } = ValidationReport.Clean;
		public ExportedModel? Export { get; init; }
		public string Error { get; init; } = string.Empty;
	}

	public sealed record SetResult(SetManifest Manifest, IReadOnlyList<ModelResult> Models, IReadOnlyList<StageUsage> Usage);

	/// <summary>
	/// Batch driver (Decision 9): plan → author parts → assemble → validate,
	/// re-authoring only the offending parts for a bounded number of rounds,
	/// then export. One asset failing never aborts the batch — it lands in the
	/// gallery as Failed/NeedsReview for the operator's accept/regenerate/refine
	/// pass.
	/// </summary>
	public sealed class SetOrchestrator
	{
		public const string ReviewStage = "3-review";

		private readonly IAnthropicGateway _gateway;
		private readonly VoxelizationConfig _config;
		private readonly IReferenceImageSource _images;
		private readonly TokenUsageTracker _usage;
		private readonly BriefExtractor _briefer;
		private readonly ModelPlanner _planner;
		private readonly ModelRefiner _refiner;
		private readonly PartAuthor _author;
		private readonly ModelAssembler _assembler;
		private readonly ModelValidator _validator;

		public SetOrchestrator(
			IAnthropicGateway gateway,
			VoxelizationConfig config,
			IReferenceImageSource images,
			IPartScriptRunner scriptRunner,
			TokenUsageTracker usage)
		{
			_gateway = gateway;
			_config = config;
			_briefer = new BriefExtractor(gateway, config);
			_images = images;
			_usage = usage;
			_planner = new ModelPlanner(gateway, config);
			_refiner = new ModelRefiner(gateway, config);
			_author = new PartAuthor(gateway, config);
			_assembler = new ModelAssembler(scriptRunner);
			_validator = new ModelValidator(config.SilhouetteIouThreshold);
		}

		public async Task<SetResult> RunAsync(SetManifest manifest, CancellationToken ct, IProgress<string>? progress = null)
		{
			// Assets are independent, so they generate concurrently; results come
			// back in manifest order. A failing asset surfaces as a Failed result,
			// never as a batch abort.
			var results = await Task.WhenAll(manifest.Assets
				.Select(asset => RunAssetAsync(manifest, asset, string.Empty, ct, progress)));

			return new SetResult(manifest, results, _usage.Snapshot());
		}

		public async Task<ModelResult> RunAssetAsync(
			SetManifest manifest,
			ManifestAsset asset,
			string refinementNote,
			CancellationToken ct,
			IProgress<string>? progress = null,
			ModelResult? previousResult = null)
		{
			// Track the last good assembly outside the loop: a late failure after we
			// already have one degrades to NeedsReview instead of throwing it away.
			AssembledModel? assembled = null;
			var report = ValidationReport.Clean;
			var brief = ReferenceBrief.None;
			var degradeError = string.Empty;

			try
			{
				var image = asset.HasReference
					? await _images.LoadAsync(asset.Reference, ct)
					: AnthropicImage.None;

				// On a re-run seeded by a previous result, reuse its accepted brief:
				// re-transcribing the reference is non-deterministic and would shift
				// the gates a passing model already cleared.
				if (previousResult != null && !previousResult.Brief.IsEmpty)
				{
					brief = previousResult.Brief;
				}
				else if (!image.IsEmpty)
				{
					progress?.Report($"{asset.Id}: transcribing the reference image...");
					brief = await _briefer.ExtractAsync(manifest, asset, image, ct);
					progress?.Report($"{asset.Id}: reference brief — {brief.Palette.Count} colours, " +
									 $"silhouette {brief.Silhouette.Size.x}x{brief.Silhouette.Size.y}");
				}

				var note = refinementNote;
				var suppressPaletteGate = refinementNote.Length > 0;
				var previousModelYaml = previousResult != null ? VModelYaml.Write(previousResult.Model) : string.Empty;

				for (var review = 0; ; review++)
				{
					try
					{
						progress?.Report($"{asset.Id}: planning...");
						var plan = await _planner.PlanAsync(
							manifest, asset, image, brief, note, ct, previousModelYaml, suppressPaletteGate);
						brief = plan.Brief;
						progress?.Report($"{asset.Id}: plan — {DescribePlan(plan.Skeleton)}");

						var model = plan.Skeleton;
						var plannedById = model.Parts
							.Where(p => p.Data is PlannedPartData)
							.ToDictionary(p => p.Id, p => (PlannedPartData)p.Data);

						foreach (var partId in plannedById.Keys)
						{
							ct.ThrowIfCancellationRequested();
							var planned = plannedById[partId];
							progress?.Report(
								$"{asset.Id}: authoring {partId} ({planned.PlannedEncoding.ToString().ToLowerInvariant()}, " +
								$"{planned.Size.x}x{planned.Size.y}x{planned.Size.z}{Note(planned)})...");
							model = await AuthorPartAsync(model, brief, partId, planned, string.Empty, ct);
						}

						(model, assembled, report) = await AssembleAndRepairAsync(
							model, brief, plannedById, asset.Id, !suppressPaletteGate, ct, progress);

						if (review >= _config.MaxReviewRounds)
						{
							break;
						}

						progress?.Report($"{asset.Id}: reviewing the result against the reference...");
						var corrections = await ReviewAsync(image, model, assembled, brief, ct);
						if (corrections.Length == 0)
						{
							progress?.Report($"{asset.Id}: review — looks faithful");
							break;
						}

						progress?.Report($"{asset.Id}: review requested corrections — re-planning:\n{corrections}");
						note = refinementNote.Length > 0 ? $"{refinementNote}\n{corrections}" : corrections;
						previousModelYaml = VModelYaml.Write(model);
					}
					catch (VoxelizationException ex) when (assembled != null)
					{
						// A later round failed, but an earlier one already produced a
						// good build — keep it (flagged NeedsReview) rather than failing.
						degradeError = Truncate(ex.Message);
						progress?.Report($"{asset.Id}: kept the last good build after a late failure — {ex}");
						break;
					}
				}

				return ExportResult(asset.Id, assembled!, brief, report, degradeError, progress);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				progress?.Report($"{asset.Id}: FAILED — {ex}");
				return new ModelResult
				{
					AssetId = asset.Id,
					Status = ModelStatus.Failed,
					Error = Truncate(ex.Message),
				};
			}
		}

		/// <summary>
		/// Applies an operator note to an already-accepted model as minimal edit
		/// operations (Decision: minor edits must not reconsider the whole model),
		/// then re-authors only the edited parts, re-assembles, re-validates and
		/// re-exports. A structural/ambiguous note escalates to a full re-plan,
		/// seeded with the previous model and brief. Untouched parts stay
		/// bit-identical because <see cref="ModelEdits.Apply"/> leaves them
		/// reference-equal and only edited parts are eligible for re-authoring.
		/// </summary>
		public async Task<ModelResult> RefineAssetAsync(
			SetManifest manifest,
			ManifestAsset asset,
			ModelResult previous,
			string note,
			CancellationToken ct,
			IProgress<string>? progress = null)
		{
			try
			{
				progress?.Report($"{asset.Id}: refining against the note:\n{note}");
				progress?.Report($"{asset.Id}: proposing edits...");
				var ops = await _refiner.ProposeAsync(previous.Model, previous.Brief, note, ct);
				progress?.Report($"{asset.Id}: refiner proposed {ops.Count} op(s):\n  " +
								 string.Join("\n  ", ops.Select(DescribeOp)));

				if (ops.Any(o => o is ReplanOp))
				{
					var reason = string.Join("; ", ops.OfType<ReplanOp>().Select(o => o.Reason));
					progress?.Report($"{asset.Id}: refine escalated to a full re-plan — {reason}");
					var combined = note.Length > 0 ? $"{note}\n{reason}" : reason;
					return await RunAssetAsync(manifest, asset, combined, ct, progress, previous);
				}

				var (model, brief, reauthors) = ModelEdits.Apply(previous.Model, previous.Brief, ops);
				progress?.Report($"{asset.Id}: applied {ops.Count} edit(s); re-authoring {reauthors.Count} part(s)...");

				foreach (var reauthor in reauthors)
				{
					ct.ThrowIfCancellationRequested();
					model = await ReauthorPartAsync(model, brief, reauthor, ct, progress);
				}

				// Only edited parts may be re-authored by the repair loop; validation
				// noise on untouched parts must not trigger their (bit-changing) redo.
				var reauthorable = EditedReauthorable(model, ModelEdits.EditedPartIds(ops));
				var (_, assembled, report) = await AssembleAndRepairAsync(
					model, brief, reauthorable, asset.Id, checkBriefPalette: false, ct, progress);

				return ExportResult(asset.Id, assembled, brief, report, string.Empty, progress);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				progress?.Report($"{asset.Id}: refine FAILED — {ex}");
				return new ModelResult
				{
					AssetId = asset.Id,
					Status = ModelStatus.Failed,
					Error = Truncate(ex.Message),
				};
			}
		}

		/// <summary>
		/// Shared tail: assemble, validate, then re-author the failing parts the
		/// caller marked as reauthorable for a bounded number of rounds. A
		/// re-authoring failure after the first assembly degrades to the last good
		/// build instead of throwing — the asset still exports for review.
		/// </summary>
		private async Task<(VoxelRigModel Model, AssembledModel Assembled, ValidationReport Report)> AssembleAndRepairAsync(
			VoxelRigModel model,
			ReferenceBrief brief,
			IReadOnlyDictionary<string, PlannedPartData> reauthorable,
			string assetId,
			bool checkBriefPalette,
			CancellationToken ct,
			IProgress<string>? progress)
		{
			progress?.Report($"{assetId}: assembling...");
			var assembled = await _assembler.AssembleAsync(model, ct);
			var report = assembled.AssemblyIssues.Merge(_validator.Validate(assembled, brief, checkBriefPalette));
			ReportOutcome(assetId, assembled, report, progress);

			for (var round = 1; round <= _config.MaxValidationRounds && !report.IsValid; round++)
			{
				var failing = report.FailingPartIds.Where(reauthorable.ContainsKey).ToList();
				if (failing.Count == 0)
				{
					break;
				}

				try
				{
					var views = RenderedViews(assembled, brief);
					foreach (var partId in failing)
					{
						ct.ThrowIfCancellationRequested();
						var issuesText = string.Join("\n", report.Issues
							.Where(i => i.PartId == partId)
							.Select(i => i.ToString()));
						progress?.Report($"{assetId}: re-authoring {partId} (round {round}) because: {issuesText}");
						model = await AuthorPartAsync(model, brief, partId, reauthorable[partId], $"{issuesText}\n\n{views}", ct);
					}
				}
				catch (VoxelizationException ex)
				{
					progress?.Report($"{assetId}: kept the last good build — re-authoring failed: {ex.Message}");
					break;
				}

				assembled = await _assembler.AssembleAsync(model, ct);
				report = assembled.AssemblyIssues.Merge(_validator.Validate(assembled, brief, checkBriefPalette));
				ReportOutcome(assetId, assembled, report, progress);
			}

			// assembled.Model is the model that produced this composition; returning
			// it keeps model/assembled consistent even after a mid-round degrade.
			return (assembled.Model, assembled, report);
		}

		private static ModelResult ExportResult(
			string assetId,
			AssembledModel assembled,
			ReferenceBrief brief,
			ValidationReport report,
			string degradeError,
			IProgress<string>? progress)
		{
			progress?.Report($"{assetId}: exporting...");
			var export = ModelExporter.Export(assembled, brief);

			return new ModelResult
			{
				AssetId = assetId,
				Status = degradeError.Length > 0 || !report.IsValid ? ModelStatus.NeedsReview : ModelStatus.Ready,
				Model = assembled.Model,
				Brief = brief,
				Assembled = assembled,
				Report = report,
				Export = export,
				Error = degradeError,
			};
		}

		/// <summary>The reauthorable window for each edited authored part, so the repair loop can redo just those.</summary>
		private static IReadOnlyDictionary<string, PlannedPartData> EditedReauthorable(
			VoxelRigModel model, IReadOnlyCollection<string> editedIds)
		{
			var reauthorable = new Dictionary<string, PlannedPartData>();
			foreach (var id in editedIds)
			{
				if (model.FindPart(id) is { Data: not (MirrorPartData or CopyPartData) } part)
				{
					reauthorable[id] = PlannedFor(part, size: null, offset: null, instructions: string.Empty);
				}
			}

			return reauthorable;
		}

		private async Task<VoxelRigModel> ReauthorPartAsync(
			VoxelRigModel model, ReferenceBrief brief, ReauthorOp op, CancellationToken ct, IProgress<string>? progress)
		{
			var part = model.FindPart(op.PartId)
					   ?? throw new VoxelizationException($"Reauthor target '{op.PartId}' is missing from the model.");
			var planned = PlannedFor(part, op.Size, op.Offset, op.Instructions);
			progress?.Report(
				$"{model.Id}/{op.PartId}: re-authoring ({planned.PlannedEncoding.ToString().ToLowerInvariant()}, " +
				$"{planned.Size.x}x{planned.Size.y}x{planned.Size.z}) — {op.Instructions}");
			return await AuthorPartAsync(model, brief, op.PartId, planned, string.Empty, ct);
		}

		/// <summary>Recovers a part's encoding/box for re-authoring, applying any resize override from the refine op.</summary>
		private static PlannedPartData PlannedFor(VoxelPart part, Vector3Int? size, Vector3Int? offset, string instructions)
		{
			var (encoding, partSize, partOffset) = part.Data switch
			{
				LayersPartData layers => (PartEncoding.Layers, layers.Size, layers.Offset),
				ScriptPartData script => (PartEncoding.Script, script.Size, script.Offset),
				PrimitivesPartData primitives => (PartEncoding.Primitives, primitives.Size, primitives.Offset),
				PlannedPartData planned => (planned.PlannedEncoding, planned.Size, planned.Offset),
				_ => (PartEncoding.Layers, Vector3Int.one, Vector3Int.zero),
			};

			return new PlannedPartData(encoding, size ?? partSize, offset ?? partOffset, instructions);
		}

		/// <summary>
		/// Spells out a single refine op — its method and every parameter — so the
		/// log records exactly what was changed and how, not just how many edits ran.
		/// </summary>
		private static string DescribeOp(ModelEditOp op) => op switch
		{
			RecolourOp o => $"recolour palette key '{o.Key}' → #{Hex(o.Colour)}",
			AddColourOp o => $"add_colour key '{o.Key}' = #{Hex(o.Colour)}",
			RemapPartColourOp o => $"remap_colour on part '{o.PartId}': key '{o.From}' → '{o.To}'",
			MovePivotOp o => $"move_pivot '{o.PartId}' by ({o.Delta.x}, {o.Delta.y}, {o.Delta.z})",
			MoveOffsetOp o => $"move_offset '{o.PartId}' by ({o.Delta.x}, {o.Delta.y}, {o.Delta.z})",
			ReauthorOp o => $"reauthor '{o.PartId}'" +
							 (o.Size is { } s ? $", resize to {s.x}x{s.y}x{s.z}" : string.Empty) +
							 (o.Offset is { } off ? $", offset ({off.x}, {off.y}, {off.z})" : string.Empty) +
							 $" — {o.Instructions}",
			DeletePartOp o => $"delete '{o.PartId}' (and any dependent children/mirrors/copies)",
			ReplanOp o => $"replan (escape hatch) — {o.Reason}",
			_ => op.ToString(),
		};

		private static string Hex(Color32 c) => $"{c.r:X2}{c.g:X2}{c.b:X2}";

		private static string Truncate(string message, int max = 500) =>
			message.Length <= max ? message : message.Substring(0, max) + " … (see log)";

		private static string DescribePlan(VoxelRigModel skeleton)
		{
			var parts = skeleton.Parts.Select(p => p.Data switch
			{
				PlannedPartData planned =>
					$"{p.Id} ({planned.PlannedEncoding.ToString().ToLowerInvariant()} {planned.Size.x}x{planned.Size.y}x{planned.Size.z}{(p.Loose ? ", loose" : "")})",
				MirrorPartData mirror => $"{p.Id} (mirror of {mirror.Source})",
				CopyPartData copy => $"{p.Id} (copy of {copy.Source})",
				_ => p.Id,
			});

			return $"{skeleton.Parts.Count} parts: {string.Join(", ", parts)}; " +
				   $"{skeleton.Palette.Count} colours; {skeleton.Poses.Count} pose(s); target {skeleton.HeightInVoxels} voxels tall";
		}

		/// <summary>
		/// ASCII views of what was actually assembled, given to re-authoring calls
		/// so the model can see its mistake instead of inferring it from an issue
		/// string. Front + side + top suffice for bilateral models (left/right
		/// mirror); asymmetric models also get the back view, since their rear can
		/// differ from what front + side imply. Bottom rarely disambiguates anything.
		/// </summary>
		private static string RenderedViews(AssembledModel assembled, ReferenceBrief brief)
		{
			var palette = assembled.Model.Palette;
			var sb = new StringBuilder();
			sb.Append(MeasuredDimensions(assembled)).Append('\n');
			sb.Append("What the WHOLE assembled model currently looks like (palette keys, top row first):\n");
			sb.Append("FRONT view (x right, y up):\n")
				.Append(VoxelProjector.Ascii(assembled.Composed, palette, ProjectionFace.Front)).Append('\n');
			sb.Append("SIDE view, from the model's right (z right towards the viewer-side, y up):\n")
				.Append(VoxelProjector.Ascii(assembled.Composed, palette, ProjectionFace.Side)).Append('\n');
			sb.Append("TOP view (x right; top row is the model's front):\n")
				.Append(VoxelProjector.Ascii(assembled.Composed, palette, ProjectionFace.Top)).Append('\n');

			if (!assembled.Model.IsBilateral)
			{
				sb.Append("BACK view, from behind the model (x runs opposite to the FRONT view, y up):\n")
					.Append(VoxelProjector.Ascii(assembled.Composed, palette, ProjectionFace.Back)).Append('\n');
			}

			if (!brief.Silhouette.IsEmpty)
			{
				sb.Append("Reference front silhouette the FRONT view must match ('#' solid):\n");
				foreach (var row in brief.Silhouette.Rows)
				{
					sb.Append(row).Append('\n');
				}
			}

			return sb.ToString().TrimEnd();
		}

		/// <summary>
		/// Measured vs target bounding box, spelled out so the reviewer judges
		/// proportions as numbers instead of inferring them from ASCII — an
		/// 11-wide x 7-long car reads fine in a front view but is obviously
		/// wrong stated as dimensions.
		/// </summary>
		private static string MeasuredDimensions(AssembledModel assembled)
		{
			var size = assembled.Composed.Size;
			var model = assembled.Model;
			var targets = new List<string> { $"{model.HeightInVoxels} tall" };
			if (model.TargetLength > 0)
			{
				targets.Add($"{model.TargetLength} long");
			}

			if (model.TargetWidth > 0)
			{
				targets.Add($"{model.TargetWidth} wide");
			}

			return $"Assembled size: {size.x} wide (x, left-right) x {size.y} tall (y) x {size.z} long " +
				   $"(z, forward) — target: {string.Join(", ", targets)}.";
		}

		private static string Note(PlannedPartData planned) =>
			planned.Note.Length > 0 ? $" — {planned.Note}" : string.Empty;

		private static void ReportOutcome(string assetId, AssembledModel assembled, ValidationReport report, IProgress<string>? progress)
		{
			var size = assembled.Composed.Size;
			progress?.Report($"{assetId}: assembled {assembled.Composed.Voxels.Count:n0} voxels ({size.x}x{size.y}x{size.z})");
			if (report.IsValid)
			{
				progress?.Report($"{assetId}: validation clean");
			}
			else
			{
				progress?.Report($"{assetId}: validation found {report.Issues.Count} issue(s):\n  " +
								 string.Join("\n  ", report.Issues.Select(i => i.ToString())));
			}
		}

		/// <summary>
		/// One vision-capable call comparing the built ASCII views (and the
		/// original reference image when there is one) against the intent.
		/// Returns an empty string when the model is approved, otherwise the
		/// reviewer's corrections — fed back into a full re-plan, since shape
		/// problems live in part sizes and pivots that re-authoring cannot touch.
		/// </summary>
		private async Task<string> ReviewAsync(
			AnthropicImage image,
			VoxelRigModel model,
			AssembledModel assembled,
			ReferenceBrief brief,
			CancellationToken ct)
		{
			var user = VoxelizationPrompts.ReviewUser(model, RenderedViews(assembled, brief), !image.IsEmpty, _config.StyleGuidance);
			var messages = new List<AnthropicMessage>
			{
				image.IsEmpty
					? new AnthropicMessage("user", user)
					: new AnthropicMessage("user", user, new[] { image }),
			};

			var response = await _gateway.SendAsync(
				ReviewStage, _config.PlanningModel, VoxelizationPrompts.ReviewSystem, messages, ct);
			return IsApproval(response) ? string.Empty : response.Trim();
		}

		private static bool IsApproval(string response)
		{
			var firstLine = response.Trim().Split('\n')[0].Trim().TrimEnd('.', '!');
			return firstLine.Equals("OK", StringComparison.OrdinalIgnoreCase);
		}

		private async Task<VoxelRigModel> AuthorPartAsync(
			VoxelRigModel model,
			ReferenceBrief brief,
			string partId,
			PlannedPartData planned,
			string feedback,
			CancellationToken ct)
		{
			var part = model.FindPart(partId)
					   ?? throw new VoxelizationException($"Part '{partId}' vanished from the model.");
			var data = await _author.AuthorAsync(model, brief, part, planned, feedback, ct);
			return model.WithPartData(partId, data);
		}
	}
}
