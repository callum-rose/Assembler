using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxels.Tests
{
	public sealed class TextureQuantizeTests
	{
		private static readonly Color32 Red = new Color32(220, 40, 40, 255);
		private static readonly Color32 Blue = new Color32(40, 40, 220, 255);

		[Test]
		public void Apply_NoSmooth_EveryTexelLandsOnPalette_NoBlendedColours()
		{
			// 8×1 strip: pure red, a 2-texel purple gradient blend across the seam, pure blue. The raw
			// blend is off-palette; after the snap every texel must be exactly one of the two swatches.
			var palette = new List<Color32> { Red, Blue };
			Color32[] row =
			{
				Red, Red, Red,
				new Color32(130, 40, 130, 255), new Color32(130, 40, 130, 255),
				Blue, Blue, Blue,
			};
			var tex = Snapshot(row, 8, 1);

			TextureQuantize.Apply(tex, palette, Opts(smooth: false));

			Color32[] outRow = ReadSrgb(tex);
			foreach (Color32 c in outRow)
			{
				Assert.IsTrue(IsSwatch(c), $"texel {c} is neither red nor blue (off-palette / blended)");
			}
			AssertSingleMonotonicBoundary(outRow);
		}

		[Test]
		public void Apply_Smooth_SoftGradientEdge_BecomesOneCleanBoundary()
		{
			// 6×4 image: left half red, right half blue, with a soft 2-column gradient at the seam. After an
			// edge-preserving smooth + snap, each row is a clean red→blue step (no ragged in-between colours).
			const int w = 6, h = 4;
			var palette = new List<Color32> { Red, Blue };
			var pixels = new Color32[w * h];
			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
				{
					Color32 c = x switch
					{
						<= 1 => Red,
						2 => new Color32(160, 40, 100, 255), // gradient toward blue
						3 => new Color32(100, 40, 160, 255),
						_ => Blue,
					};
					pixels[y * w + x] = c;
				}
			}
			var tex = Snapshot(pixels, w, h);

			TextureQuantize.Apply(tex, palette, Opts(smooth: true));

			Color32[] outPixels = ReadSrgb(tex);
			var distinct = new HashSet<int>();
			for (int y = 0; y < h; y++)
			{
				var rowSlice = new Color32[w];
				for (int x = 0; x < w; x++)
				{
					Color32 c = outPixels[y * w + x];
					Assert.IsTrue(IsSwatch(c), $"texel {c} at ({x},{y}) is off-palette");
					rowSlice[x] = c;
					distinct.Add(Pack(Nearest(c)));
				}
				AssertSingleMonotonicBoundary(rowSlice);
			}
			Assert.AreEqual(2, distinct.Count, "both stripe colours must survive");
		}

		[Test]
		public void Apply_NearNeutralTexel_DoesNotGainChroma()
		{
			// A faintly-warm light grey, with a neutral grey and a saturated pink in the palette. The shared
			// chroma-penalty path must keep it neutral rather than flipping it onto the saturated swatch.
			var neutral = new Color32(185, 185, 185, 255);
			var pink = new Color32(220, 120, 150, 255);
			var palette = new List<Color32> { neutral, pink };
			var tex = Snapshot(new[] { new Color32(200, 198, 196, 255) }, 1, 1);

			TextureQuantize.Apply(tex, palette, Opts(smooth: false));

			Color32 result = ReadSrgb(tex)[0];
			Assert.IsTrue(Approx(result, neutral), $"near-neutral texel snapped to {result}, expected neutral grey");
		}

		[Test]
		public void Apply_EmptyPalette_LeavesTextureUntouched()
		{
			Color32[] row = { Red, Blue };
			var tex = Snapshot(row, 2, 1);

			TextureQuantize.Apply(tex, new List<Color32>(), Opts(smooth: true));

			Color32[] outRow = ReadSrgb(tex);
			Assert.IsTrue(Approx(outRow[0], Red) && Approx(outRow[1], Blue), "empty palette must be a no-op");
		}

		// --- Helpers -------------------------------------------------------

		private static TextureQuantize.Options Opts(bool smooth) =>
			TextureQuantize.Options.From(new VoxPipelineSettings
			{
				textureSmooth = smooth,
				textureSmoothRadius = 2,
				textureSmoothRange = 0.10f,
			});

		// sRGB Color32 grid → the snapshot's LINEAR buffer (mirroring TextureSnapshot.Capture).
		private static ObjToVoxConverter.TextureSnapshot Snapshot(Color32[] srgb, int width, int height)
		{
			var linear = new Color[srgb.Length];
			for (int i = 0; i < srgb.Length; i++)
			{
				linear[i] = ((Color)srgb[i]).linear;
			}
			return new ObjToVoxConverter.TextureSnapshot(linear, width, height);
		}

		// Read the linear buffer back out as sRGB Color32 (mirroring the converter's SampleBilinear(...).gamma).
		private static Color32[] ReadSrgb(ObjToVoxConverter.TextureSnapshot tex)
		{
			Color[] linear = tex.ClonePixels();
			var srgb = new Color32[linear.Length];
			for (int i = 0; i < linear.Length; i++)
			{
				srgb[i] = linear[i].gamma;
			}
			return srgb;
		}

		private static bool IsSwatch(Color32 c) => Approx(c, Red) || Approx(c, Blue);

		private static Color32 Nearest(Color32 c) => Approx(c, Red) ? Red : Blue;

		// Each strip must be red* followed by blue* (a single clean boundary), never red blue red.
		private static void AssertSingleMonotonicBoundary(Color32[] strip)
		{
			bool seenBlue = false;
			foreach (Color32 c in strip)
			{
				bool isBlue = Approx(c, Blue);
				if (isBlue)
				{
					seenBlue = true;
				}
				else
				{
					Assert.IsFalse(seenBlue, "stripe colour reappears after the boundary (ragged, not monotonic)");
				}
			}
		}

		private static int Pack(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;

		private static bool Approx(Color32 a, Color32 b, int tolerance = 4) =>
			Mathf.Abs(a.r - b.r) <= tolerance &&
			Mathf.Abs(a.g - b.g) <= tolerance &&
			Mathf.Abs(a.b - b.b) <= tolerance;
	}
}
