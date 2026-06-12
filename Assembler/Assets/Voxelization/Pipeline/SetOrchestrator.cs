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
		private readonly PartAuthor _author;
		private readonly ModelAssembler _assembler;
		private readonly ModelValidator _validator;
		private readonly LocalEditor _editor;

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
			_author = new PartAuthor(gateway, config);
			_assembler = new ModelAssembler(scriptRunner);
			_validator = new ModelValidator(config.SilhouetteIouThreshold);
			_editor = new LocalEditor(gateway, config);
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
			IProgress<string>? progress = null)
		{
			try
			{
				var image = asset.HasReference
					? await _images.LoadAsync(asset.Reference, ct)
					: AnthropicImage.None;

				var brief = ReferenceBrief.None;
				if (!image.IsEmpty)
				{
					progress?.Report($"{asset.Id}: transcribing the reference image...");
					brief = await _briefer.ExtractAsync(manifest, asset, image, ct);
					progress?.Report($"{asset.Id}: reference brief — {brief.Palette.Count} colours, " +
									 $"silhouette {brief.Silhouette.Size.x}x{brief.Silhouette.Size.y}");
				}

				var note = refinementNote;
				ModelPlan plan = null!;
				VoxelRigModel model = null!;
				AssembledModel assembled = null!;
				ValidationReport report = null!;

				for (var review = 0; ; review++)
				{
					progress?.Report($"{asset.Id}: planning...");
					plan = await _planner.PlanAsync(manifest, asset, image, brief, note, ct);
					progress?.Report($"{asset.Id}: plan — {DescribePlan(plan.Skeleton)}");

					model = plan.Skeleton;
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
						model = await AuthorPartAsync(model, plan.Brief, partId, planned, string.Empty, ct);
					}

					progress?.Report($"{asset.Id}: assembling...");
					assembled = await _assembler.AssembleAsync(model, ct);
					report = assembled.AssemblyIssues.Merge(_validator.Validate(assembled, plan.Brief));
					ReportOutcome(asset.Id, assembled, report, progress);

					for (var round = 1; round <= _config.MaxValidationRounds && !report.IsValid; round++)
					{
						var failing = report.FailingPartIds.Where(plannedById.ContainsKey).ToList();
						if (failing.Count == 0)
						{
							break;
						}

						var views = RenderedViews(assembled, plan.Brief);
						foreach (var partId in failing)
						{
							ct.ThrowIfCancellationRequested();
							var issuesText = string.Join("\n", report.Issues
								.Where(i => i.PartId == partId)
								.Select(i => i.ToString()));
							progress?.Report($"{asset.Id}: re-authoring {partId} (round {round}) because: {issuesText}");
							model = await AuthorPartAsync(model, plan.Brief, partId, plannedById[partId], $"{issuesText}\n\n{views}", ct);
						}

						assembled = await _assembler.AssembleAsync(model, ct);
						report = assembled.AssemblyIssues.Merge(_validator.Validate(assembled, plan.Brief));
						ReportOutcome(asset.Id, assembled, report, progress);
					}

					if (review >= _config.MaxReviewRounds)
					{
						break;
					}

					progress?.Report($"{asset.Id}: reviewing the result against the reference...");
					var corrections = await ReviewAsync(image, model, assembled, plan.Brief, ct);
					if (corrections.Length == 0)
					{
						progress?.Report($"{asset.Id}: review — looks faithful");
						break;
					}

					progress?.Report($"{asset.Id}: review requested corrections — re-planning:\n{corrections}");
					note = refinementNote.Length > 0 ? $"{refinementNote}\n{corrections}" : corrections;
				}

				progress?.Report($"{asset.Id}: exporting...");
				var export = ModelExporter.Export(assembled, plan.Brief);

				return new ModelResult
				{
					AssetId = asset.Id,
					Status = report.IsValid ? ModelStatus.Ready : ModelStatus.NeedsReview,
					Model = model,
					Brief = plan.Brief,
					Assembled = assembled,
					Report = report,
					Export = export,
				};
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				progress?.Report($"{asset.Id}: FAILED — {ex.Message}");
				return new ModelResult
				{
					AssetId = asset.Id,
					Status = ModelStatus.Failed,
					Error = ex.Message,
				};
			}
		}

		/// <summary>
		/// The lightweight refine path (issue 307): apply a short operator note to
		/// an already-generated model as a targeted local edit — palette recolours
		/// plus per-part moves/reshapes — re-authoring only the parts the note
		/// touches and never re-planning. Falls back nowhere: an empty
		/// interpretation returns the previous result unchanged so the operator can
		/// choose a full regenerate. The full re-plan that <see cref="RunAssetAsync"/>
		/// performs is what the plan gates frequently reject; skipping it is why a
		/// minor edit no longer errors out.
		/// </summary>
		public async Task<ModelResult> RefineAssetAsync(
			ModelResult previous,
			string note,
			CancellationToken ct,
			IProgress<string>? progress = null)
		{
			var assetId = previous.AssetId;
			try
			{
				var model = previous.Model;
				var brief = previous.Brief;
				var views = previous.Assembled is { } prior ? RenderedViews(prior, brief) : string.Empty;

				progress?.Report($"{assetId}: interpreting the refinement note...");
				var edit = await _editor.InterpretAsync(model, views, note, ct);
				if (edit.IsEmpty)
				{
					progress?.Report($"{assetId}: the note needs a full regenerate — no local edit applied.");
					return previous;
				}

				if (edit.Palette.Count > 0)
				{
					model = model with { Palette = ApplyPaletteEdits(model.Palette, edit.Palette) };
					progress?.Report($"{assetId}: recoloured {string.Join(", ", edit.Palette.Select(p => p.Key))}");
				}

				// Deterministic pivot/offset/size moves first, so any re-author sees
				// the final placement and window. A note (or a size change, which
				// leaves the old grid the wrong shape) re-authors that one part.
				var toReauthor = new List<(string Id, string Note)>();
				foreach (var part in edit.Parts)
				{
					model = ApplyPartMove(model, part);
					if (part.Note.Length > 0 || part.Size.HasValue)
					{
						toReauthor.Add((ResolveAuthoredSource(model, part.Id), EditNote(part)));
					}
				}

				foreach (var group in toReauthor.GroupBy(r => r.Id))
				{
					ct.ThrowIfCancellationRequested();
					var partId = group.Key;
					var partNote = string.Join("; ", group.Select(r => r.Note).Where(n => n.Length > 0));
					var planned = PlannedFrom(model.FindPart(partId)?.Data, partNote);
					if (planned == null)
					{
						progress?.Report($"{assetId}: '{partId}' has no authored geometry to re-author — skipped.");
						continue;
					}

					progress?.Report($"{assetId}: re-authoring {partId}...");
					// The note rides in planned.Note (the author's guidance channel),
					// so the feedback slot — framed as a validation fix — stays empty.
					model = await AuthorPartAsync(model, brief, partId, planned, string.Empty, ct);
				}

				progress?.Report($"{assetId}: assembling...");
				var assembled = await _assembler.AssembleAsync(model, ct);
				var report = assembled.AssemblyIssues.Merge(_validator.Validate(assembled, brief));
				ReportOutcome(assetId, assembled, report, progress);

				progress?.Report($"{assetId}: exporting...");
				var export = ModelExporter.Export(assembled, brief);

				return new ModelResult
				{
					AssetId = assetId,
					Status = report.IsValid ? ModelStatus.Ready : ModelStatus.NeedsReview,
					Model = model,
					Brief = brief,
					Assembled = assembled,
					Report = report,
					Export = export,
				};
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				progress?.Report($"{assetId}: FAILED — {ex.Message}");
				return new ModelResult { AssetId = assetId, Status = ModelStatus.Failed, Error = ex.Message };
			}
		}

		private static IReadOnlyList<PaletteEntry> ApplyPaletteEdits(
			IReadOnlyList<PaletteEntry> palette, IReadOnlyList<PaletteEdit> edits)
		{
			var result = palette.ToList();
			foreach (var edit in edits)
			{
				var index = result.FindIndex(e => e.Key == edit.Key);
				if (index >= 0)
				{
					result[index] = result[index] with { Colour = edit.Colour };
				}
				else
				{
					result.Add(new PaletteEntry(edit.Key, edit.Colour));
				}
			}

			return result;
		}

		private static VoxelRigModel ApplyPartMove(VoxelRigModel model, PartEdit edit)
		{
			var part = model.FindPart(edit.Id);
			if (part == null)
			{
				return model;
			}

			var moved = part;
			if (edit.Pivot is { } pivot)
			{
				moved = moved with { Pivot = pivot };
			}

			if (edit.Offset.HasValue || edit.Size.HasValue)
			{
				moved = moved with { Data = ApplyGeometry(moved.Data, edit.Offset, edit.Size) };
			}

			return model.WithPart(moved);
		}

		private static PartData ApplyGeometry(PartData data, Vector3Int? offset, Vector3Int? size) => data switch
		{
			LayersPartData l => l with { Offset = offset ?? l.Offset, Size = size ?? l.Size },
			PrimitivesPartData p => p with { Offset = offset ?? p.Offset, Size = size ?? p.Size },
			ScriptPartData s => s with { Offset = offset ?? s.Offset, Size = size ?? s.Size },
			PlannedPartData pl => pl with { Offset = offset ?? pl.Offset, Size = size ?? pl.Size },
			_ => data,
		};

		private static PlannedPartData? PlannedFrom(PartData? data, string note) => data switch
		{
			LayersPartData l => new PlannedPartData(PartEncoding.Layers, l.Size, l.Offset, note),
			PrimitivesPartData p => new PlannedPartData(PartEncoding.Primitives, p.Size, p.Offset, note),
			ScriptPartData s => new PlannedPartData(PartEncoding.Script, s.Size, s.Offset, note),
			PlannedPartData pl => pl with { Note = note.Length > 0 ? note : pl.Note },
			_ => null,
		};

		/// <summary>
		/// A note/resize on a mirror or copy part really targets the authored
		/// geometry it reuses, so follow the source chain to the part that actually
		/// holds the grid.
		/// </summary>
		private static string ResolveAuthoredSource(VoxelRigModel model, string id)
		{
			var seen = new HashSet<string>();
			var current = id;
			while (seen.Add(current) && model.FindPart(current)?.Data is var data && data is MirrorPartData or CopyPartData)
			{
				current = data switch
				{
					MirrorPartData mirror => mirror.Source,
					CopyPartData copy => copy.Source,
					_ => current,
				};
			}

			return current;
		}

		private static string EditNote(PartEdit edit)
		{
			var resized = edit.Size is { } size
				? $"resized to {YamlNodes.Vector(size)} — rebuild geometry to fill the new window"
				: string.Empty;
			return (edit.Note, resized) switch
			{
				({ Length: > 0 }, { Length: > 0 }) => $"{edit.Note} ({resized})",
				({ Length: > 0 }, _) => edit.Note,
				_ => resized,
			};
		}

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
