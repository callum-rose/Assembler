using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
		private readonly VoxelizationConfig _config;
		private readonly IReferenceImageSource _images;
		private readonly TokenUsageTracker _usage;
		private readonly ModelPlanner _planner;
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
			_config = config;
			_images = images;
			_usage = usage;
			_planner = new ModelPlanner(gateway, config);
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
			IProgress<string>? progress = null)
		{
			try
			{
				progress?.Report($"{asset.Id}: planning...");
				var image = asset.HasReference
					? await _images.LoadAsync(asset.Reference, ct)
					: Assembler.Anthropic.AnthropicImage.None;
				var plan = await _planner.PlanAsync(manifest, asset, image, refinementNote, ct);
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
					model = await AuthorPartAsync(model, plan.Brief, partId, planned, string.Empty, ct);
				}

				progress?.Report($"{asset.Id}: assembling...");
				var assembled = await _assembler.AssembleAsync(model, ct);
				var report = assembled.AssemblyIssues.Merge(_validator.Validate(assembled, plan.Brief));
				ReportOutcome(asset.Id, assembled, report, progress);

				for (var round = 1; round <= _config.MaxValidationRounds && !report.IsValid; round++)
				{
					var failing = report.FailingPartIds.Where(plannedById.ContainsKey).ToList();
					if (failing.Count == 0)
					{
						break;
					}

					foreach (var partId in failing)
					{
						ct.ThrowIfCancellationRequested();
						var feedback = string.Join("\n", report.Issues
							.Where(i => i.PartId == partId)
							.Select(i => i.ToString()));
						progress?.Report($"{asset.Id}: re-authoring {partId} (round {round}) because: {feedback}");
						model = await AuthorPartAsync(model, plan.Brief, partId, plannedById[partId], feedback, ct);
					}

					assembled = await _assembler.AssembleAsync(model, ct);
					report = assembled.AssemblyIssues.Merge(_validator.Validate(assembled, plan.Brief));
					ReportOutcome(asset.Id, assembled, report, progress);
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

		private static string DescribePlan(VoxelRigModel skeleton)
		{
			var parts = skeleton.Parts.Select(p => p.Data switch
			{
				PlannedPartData planned =>
					$"{p.Id} ({planned.PlannedEncoding.ToString().ToLowerInvariant()} {planned.Size.x}x{planned.Size.y}x{planned.Size.z}{(p.Loose ? ", loose" : "")})",
				MirrorPartData mirror => $"{p.Id} (mirror of {mirror.Source})",
				_ => p.Id,
			});

			return $"{skeleton.Parts.Count} parts: {string.Join(", ", parts)}; " +
				   $"{skeleton.Palette.Count} colours; {skeleton.Poses.Count} pose(s); target {skeleton.HeightInVoxels} voxels tall";
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
