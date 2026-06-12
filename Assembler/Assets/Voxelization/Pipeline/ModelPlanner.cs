using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;

namespace Assembler.Voxelization
{
	public sealed record ModelPlan(VoxelRigModel Skeleton, ReferenceBrief Brief);

	/// <summary>
	/// Stage 1: one call per model producing the part skeleton (and, iff a
	/// reference image is attached, the reference brief — the only vision call
	/// in the pipeline). Code re-anchors whatever came back to the manifest's
	/// unit/height and demotes over-budget layers parts to scripts, so the
	/// scale bible and the voxel budget hold regardless of what the model said.
	/// </summary>
	public sealed class ModelPlanner
	{
		public const string Stage = "1-planning";

		/// <summary>Total planning calls per asset: the first plan plus feedback rounds for parse/geometry failures.</summary>
		public const int MaxAttempts = 3;

		private readonly IAnthropicGateway _gateway;
		private readonly VoxelizationConfig _config;

		public ModelPlanner(IAnthropicGateway gateway, VoxelizationConfig config)
		{
			_gateway = gateway;
			_config = config;
		}

		public async Task<ModelPlan> PlanAsync(
			SetManifest manifest,
			ManifestAsset asset,
			AnthropicImage referenceImage,
			string refinementNote,
			CancellationToken ct)
		{
			var hasImage = !referenceImage.IsEmpty;
			var userText = VoxelizationPrompts.PlanningUser(manifest, asset, hasImage, refinementNote);
			var messages = new List<AnthropicMessage>
			{
				hasImage
					? new AnthropicMessage("user", userText, new[] { referenceImage })
					: new AnthropicMessage("user", userText),
			};

			for (var attempt = 1; ; attempt++)
			{
				var response = await _gateway.SendAsync(
					Stage, _config.PlanningModel, VoxelizationPrompts.PlanningSystem(_config), messages, ct).ConfigureAwait(false);

				var (plan, feedback) = TryParse(response, manifest, asset, hasImage);
				if (plan != null)
				{
					return plan;
				}

				if (attempt >= MaxAttempts)
				{
					throw new VoxelizationException($"Planning '{asset.Id}' failed after {MaxAttempts} attempts: {feedback}");
				}

				messages.Add(new AnthropicMessage("assistant", response));
				messages.Add(new AnthropicMessage("user", feedback));
			}
		}

		private (ModelPlan? Plan, string Feedback) TryParse(string response, SetManifest manifest, ManifestAsset asset, bool hasImage)
		{
			ModelPlan plan;
			try
			{
				plan = Parse(response, manifest, asset, hasImage);
			}
			catch (FormatException ex)
			{
				return (null, $"That plan could not be parsed: {ex.Message}\nEmit the corrected fenced block(s).");
			}

			var geometryErrors = PlanGeometryChecks.Errors(plan.Skeleton);
			return geometryErrors.Count == 0
				? (plan, string.Empty)
				: (null,
					"That skeleton can never assemble bilaterally symmetric — these were rejected by deterministic geometry checks:\n- " +
					string.Join("\n- ", geometryErrors) +
					"\nFix the skeleton and emit the corrected fenced block(s).");
		}

		private ModelPlan Parse(string response, SetManifest manifest, ManifestAsset asset, bool hasImage)
		{
			var vmodelYaml = FencedBlockExtractor.Extract(response, "vmodel")
							 ?? throw new FormatException("Response contained no ```vmodel fenced block.");
			var skeleton = VModelYaml.Read(vmodelYaml);
			if (skeleton.Parts.Count == 0)
			{
				throw new FormatException("The plan declared no parts.");
			}

			if (skeleton.Palette.Count == 0)
			{
				throw new FormatException("The plan declared no palette.");
			}

			// The manifest, not the plan, owns identity, scale, and symmetry.
			skeleton = skeleton with
			{
				Id = asset.Id,
				Unit = manifest.Unit,
				RealWorldHeight = asset.RealWorldHeight,
				Rigged = asset.Rig,
				Symmetry = asset.Symmetry,
				Parts = skeleton.Parts.Select(EnforceBudget).ToArray(),
			};

			var brief = ReferenceBrief.None;
			if (hasImage)
			{
				var briefYaml = FencedBlockExtractor.Extract(response, "brief")
								?? throw new FormatException("A reference image was attached but the response contained no ```brief fenced block.");
				brief = ReferenceBriefYaml.Read(briefYaml) with { Source = asset.Reference };
				if (skeleton.IsBilateral)
				{
					brief = SymmetrizeSilhouette(brief);
				}
			}

			return new ModelPlan(skeleton, brief);
		}

		/// <summary>
		/// A lopsided vision read of a bilateral subject would poison both the
		/// authoring guidance and the silhouette IoU oracle, so the silhouette is
		/// forced symmetric in code: each row becomes the union of itself and its
		/// reflection.
		/// </summary>
		private static ReferenceBrief SymmetrizeSilhouette(ReferenceBrief brief)
		{
			if (brief.Silhouette.IsEmpty)
			{
				return brief;
			}

			var rows = brief.Silhouette.Rows
				.Select(row => new string(Enumerable.Range(0, row.Length)
					.Select(i => row[i] == '#' || row[row.Length - 1 - i] == '#' ? '#' : '.')
					.ToArray()))
				.ToArray();

			return brief with { Silhouette = brief.Silhouette with { Rows = rows } };
		}

		private VoxelPart EnforceBudget(VoxelPart part)
		{
			if (part.Data is not PlannedPartData planned || planned.PlannedEncoding != PartEncoding.Layers)
			{
				return part;
			}

			var volume = planned.Size.x * planned.Size.y * planned.Size.z;
			return volume <= _config.PartVoxelBudget
				? part
				: part with
				{
					Data = planned with
					{
						PlannedEncoding = PartEncoding.Script,
						Note = planned.Note.Length > 0
							? planned.Note + " (demoted to script: declared volume exceeds the layers budget)"
							: "demoted to script: declared volume exceeds the layers budget",
					},
				};
		}
	}
}
