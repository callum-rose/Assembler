using System.Collections.Generic;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxels
{
	/// <summary>
	/// C2 — edge-preserving palette snap in <b>texture</b> space, run before voxelizing. The default
	/// per-voxel <see cref="PaletteSnap"/> snaps each voxel in isolation <i>after</i> all spatial
	/// context is gone, so a soft gradient edge in the source render becomes a ragged palette boundary.
	/// This quantizes the source texture in 2D first — an optional edge-preserving (Oklab bilateral)
	/// smooth that flattens within-region shading while keeping colour edges, then a nearest-master-swatch
	/// snap (the shared <see cref="PaletteSnapper"/>). The voxelizer then samples an already-flat,
	/// already-on-palette, crisp-edged texture, so colour boundaries come out straight regardless of how
	/// smooth the source render was. The voxel-space <see cref="PaletteSnap"/> stays on as a cheap
	/// idempotent backstop (it re-snaps any colour the converter's bilinear/averaging blended at a
	/// boundary). Only textured models are affected; flat-colour models are handled by the voxel-space snap.
	///
	/// Pure managed work over the snapshot's pixel buffer — no Unity main-thread APIs — so it is safe to
	/// run on the conversion's background thread.
	/// </summary>
	public static class TextureQuantize
	{
		public readonly struct Options
		{
			/// <summary>Run the edge-preserving bilateral smooth before snapping.</summary>
			public bool Smooth { get; init; }

			/// <summary>Bilateral radius in texels (window is (2r+1)²).</summary>
			public int SmoothRadius { get; init; }

			/// <summary>
			/// Edge threshold in Oklab: neighbours more perceptually distant than ~this are treated as
			/// across an edge and contribute little to the blend (the range sigma of the bilateral).
			/// </summary>
			public float SmoothRange { get; init; }

			public static Options From(VoxPipelineSettings settings) => new()
			{
				Smooth = settings.textureSmooth,
				SmoothRadius = Mathf.Clamp(settings.textureSmoothRadius, 1, 4),
				SmoothRange = Mathf.Max(1e-3f, settings.textureSmoothRange),
			};
		}

		/// <summary>Quantizes the model's texture in place. No-op for flat-colour (untextured) models.</summary>
		public static void Apply(
			ObjToVoxConverter.LoadedModel model, IReadOnlyList<Color32> palette, Options options)
		{
			if (model.Colors.Texture is { } texture)
			{
				Apply(texture, palette, options);
			}
		}

		/// <summary>Quantizes a single texture snapshot in place: optional smooth, then palette snap.</summary>
		public static void Apply(
			ObjToVoxConverter.TextureSnapshot texture, IReadOnlyList<Color32> palette, Options options)
		{
			var snapper = new PaletteSnapper(palette);
			if (snapper.IsEmpty)
			{
				return;
			}

			int width = texture.Width;
			int height = texture.Height;
			if (width <= 0 || height <= 0)
			{
				return;
			}

			Color[] pixels = texture.ClonePixels();
			OklabColor[] lab = ComputeLab(pixels);

			Color[] working = options.Smooth
				? BilateralSmooth(pixels, lab, width, height, options.SmoothRadius, options.SmoothRange)
				: pixels;

			for (int i = 0; i < working.Length; i++)
			{
				// The buffer is LINEAR; the palette is sRGB (Color32). Re-encode to gamma to snap in the
				// same space the .vox palette and the voxel-space snap use, then store back as linear so
				// the converter's SampleBilinear(...).gamma reproduces the chosen swatch.
				Color32 srgb = working[i].gamma;
				working[i] = ((Color)snapper.Nearest(srgb)).linear;
			}

			texture.ReplacePixels(working);
		}

		// Per-pixel Oklab, precomputed once so the bilateral's range term is a cheap squared-distance
		// lookup rather than an sRGB→Oklab conversion per tap. Mirrors the snap's gamma re-encode so the
		// edge metric matches the swatch metric.
		private static OklabColor[] ComputeLab(Color[] linearPixels)
		{
			var lab = new OklabColor[linearPixels.Length];
			for (int i = 0; i < linearPixels.Length; i++)
			{
				lab[i] = OklabColor.FromColor32(linearPixels[i].gamma);
			}
			return lab;
		}

		// Joint bilateral filter: spatial Gaussian × range Gaussian, the range measured in Oklab. Blends
		// neighbours that are both nearby AND perceptually similar, so within-region shading flattens while
		// colour edges (large Oklab gaps) survive. Accumulation is in linear space (physically correct
		// averaging, and what the converter samples). Out-of-bounds taps are skipped (edge clamp).
		private static Color[] BilateralSmooth(
			Color[] src, OklabColor[] lab, int width, int height, int radius, float rangeSigma)
		{
			var dst = new Color[src.Length];
			float spatialSigma = Mathf.Max(1f, radius);
			float spatialDenom = 2f * spatialSigma * spatialSigma;
			float rangeDenom = 2f * rangeSigma * rangeSigma;

			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					int centre = y * width + x;
					OklabColor centreLab = lab[centre];
					float accR = 0f, accG = 0f, accB = 0f, accW = 0f;

					for (int dy = -radius; dy <= radius; dy++)
					{
						int ny = y + dy;
						if (ny < 0 || ny >= height)
						{
							continue;
						}
						for (int dx = -radius; dx <= radius; dx++)
						{
							int nx = x + dx;
							if (nx < 0 || nx >= width)
							{
								continue;
							}

							int neighbour = ny * width + nx;
							float spatial = (dx * dx + dy * dy) / spatialDenom;
							float range = lab[neighbour].SquaredDistanceTo(centreLab) / rangeDenom;
							float weight = Mathf.Exp(-(spatial + range));

							Color s = src[neighbour];
							accR += s.r * weight;
							accG += s.g * weight;
							accB += s.b * weight;
							accW += weight;
						}
					}

					dst[centre] = accW > 0f
						? new Color(accR / accW, accG / accW, accB / accW, src[centre].a)
						: src[centre];
				}
			}
			return dst;
		}
	}
}
