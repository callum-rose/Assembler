using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;
using UnityEngine;

namespace Assembler.Voxelization
{
	/// <summary>
	/// Stage 1a, deterministic path: reads the two authoritative brief fields —
	/// the silhouette occupancy mask and the colour palette — straight from the
	/// reference pixels (see <see cref="ReferenceImageAnalysis"/>) instead of from
	/// a vision call. Reference images are plain-background flat-colour cartoon
	/// art, so the silhouette is a thresholding problem, not a reasoning one; doing
	/// it in code buys reproducibility, zero token cost, pixel-exact limb/body gaps,
	/// and removes the vision path's symmetrise/trim/parse-retry scaffolding.
	///
	/// The semantic fields (proportions, signature features) are advisory only; an
	/// optional slim vision call fills them when
	/// <see cref="VoxelizationConfig.ExtractSemanticBriefFields"/> is set, otherwise
	/// they are left empty and the pipeline proceeds without them.
	/// </summary>
	public sealed class DeterministicBriefExtractor : IBriefExtractor
	{
		private readonly IAnthropicGateway _gateway;
		private readonly VoxelizationConfig _config;
		private readonly int _maxPaletteColours;

		public DeterministicBriefExtractor(IAnthropicGateway gateway, VoxelizationConfig config, int maxPaletteColours = 12)
		{
			_gateway = gateway;
			_config = config;
			_maxPaletteColours = maxPaletteColours;
		}

		public async Task<ReferenceBrief> ExtractAsync(
			SetManifest manifest,
			ManifestAsset asset,
			IReadOnlyList<(ReferenceImage Label, AnthropicImage Image)> images,
			CancellationToken ct)
		{
			if (images.Count == 0)
			{
				return ReferenceBrief.None;
			}

			// Decode once: the palette reads across EVERY face, while each silhouette
			// reads only the image labelled with its (co-axially deduped) face.
			var decoded = images
				.Select(i => (i.Label, Pixels: Decode(i.Image, asset.Id, i.Label.Face)))
				.ToList();

			var palette = ReferenceImageAnalysis.Palette(
				decoded.Select(d => d.Pixels),
				_maxPaletteColours,
				_config.BackgroundColourTolerance,
				mergeDistance: 0.06f);

			var rows = Mathf.Max(1, asset.Height);
			var silhouettes = ProjectionFaceInfo.DedupeFaces(decoded.Select(d => d.Label.Face))
				.Select(face => ReferenceImageAnalysis.Silhouette(
					face,
					decoded.First(d => string.Equals(d.Label.Face, face, StringComparison.OrdinalIgnoreCase)).Pixels,
					rows,
					_config.SilhouetteCellCoverage,
					_config.BackgroundColourTolerance))
				.ToList();

			var brief = new ReferenceBrief
			{
				Source = asset.Id,
				Palette = palette,
				Silhouettes = silhouettes,
			};

			return _config.ExtractSemanticBriefFields
				? await AddSemanticFieldsAsync(brief, asset, decoded.Select(d => d.Label).ToList(),
					images.Select(i => i.Image).ToArray(), ct).ConfigureAwait(false)
				: brief;
		}

		/// <summary>
		/// One slim vision call for the advisory proportions/signature-features.
		/// Best-effort: a malformed reply leaves those fields empty rather than
		/// failing the asset, since they only guide authoring and never gate it —
		/// so there is no parse-retry loop here.
		/// </summary>
		private async Task<ReferenceBrief> AddSemanticFieldsAsync(
			ReferenceBrief brief,
			ManifestAsset asset,
			IReadOnlyList<ReferenceImage> labels,
			AnthropicImage[] attachments,
			CancellationToken ct)
		{
			var messages = new List<AnthropicMessage>
			{
				new("user", VoxelizationPrompts.BriefSemanticUser(asset, labels), attachments),
			};

			var response = await _gateway.SendAsync(
				BriefExtractor.Stage, _config.PlanningModel, VoxelizationPrompts.BriefSemanticSystem, messages, ct)
				.ConfigureAwait(false);

			var yaml = FencedBlockExtractor.Extract(response, "brief");
			if (yaml == null)
			{
				return brief;
			}

			try
			{
				var semantic = ReferenceBriefYaml.Read(yaml);
				return brief with
				{
					Proportions = semantic.Proportions,
					SignatureFeatures = semantic.SignatureFeatures,
				};
			}
			catch (FormatException)
			{
				return brief;
			}
		}

		/// <summary>Decodes encoded image bytes to pixels (GetPixels32 order, row 0 = bottom).</summary>
		private static ReferenceImageAnalysis.Pixels Decode(AnthropicImage image, string assetId, string face)
		{
			var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
			try
			{
				if (!ImageConversion.LoadImage(texture, image.Data, markNonReadable: false))
				{
					throw new VoxelizationException(
						$"Reference image for asset '{assetId}' ({face} view) could not be decoded as an image.");
				}

				return new ReferenceImageAnalysis.Pixels(texture.GetPixels32(), texture.width, texture.height);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(texture);
			}
		}
	}
}
