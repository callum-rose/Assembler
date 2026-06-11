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

			for (var attempt = 0; ; attempt++)
			{
				var response = await _gateway.SendAsync(
					Stage, _config.PlanningModel, VoxelizationPrompts.PlanningSystem(_config), messages, ct).ConfigureAwait(false);

				try
				{
					return Parse(response, manifest, asset, hasImage);
				}
				catch (FormatException ex) when (attempt == 0)
				{
					messages.Add(new AnthropicMessage("assistant", response));
					messages.Add(new AnthropicMessage("user",
						$"That plan could not be parsed: {ex.Message}\nEmit the corrected fenced block(s)."));
				}
				catch (FormatException ex)
				{
					throw new VoxelizationException($"Planning '{asset.Id}' failed: {ex.Message}", ex);
				}
			}
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
			}

			return new ModelPlan(skeleton, brief);
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
