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
			var results = new List<ModelResult>();
			foreach (var asset in manifest.Assets)
			{
				ct.ThrowIfCancellationRequested();
				results.Add(await RunAssetAsync(manifest, asset, string.Empty, ct, progress));
			}

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

				var model = plan.Skeleton;
				var plannedById = model.Parts
					.Where(p => p.Data is PlannedPartData)
					.ToDictionary(p => p.Id, p => (PlannedPartData)p.Data);

				foreach (var partId in plannedById.Keys)
				{
					ct.ThrowIfCancellationRequested();
					progress?.Report($"{asset.Id}: authoring {partId}...");
					model = await AuthorPartAsync(model, plan.Brief, partId, plannedById[partId], string.Empty, ct);
				}

				progress?.Report($"{asset.Id}: assembling...");
				var assembled = await _assembler.AssembleAsync(model, ct);
				var report = assembled.AssemblyIssues.Merge(_validator.Validate(assembled, plan.Brief));

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
						progress?.Report($"{asset.Id}: re-authoring {partId} (round {round})...");
						var feedback = string.Join("\n", report.Issues
							.Where(i => i.PartId == partId)
							.Select(i => i.ToString()));
						model = await AuthorPartAsync(model, plan.Brief, partId, plannedById[partId], feedback, ct);
					}

					assembled = await _assembler.AssembleAsync(model, ct);
					report = assembled.AssemblyIssues.Merge(_validator.Validate(assembled, plan.Brief));
				}

				progress?.Report($"{asset.Id}: exporting...");
				var export = ModelExporter.Export(assembled);

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
