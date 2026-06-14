using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assembler.Voxelization
{
	/// <summary>
	/// Deterministic pixel analysis of a decoded reference image. Under the
	/// pipeline's plain-background, flat-colour cartoon constraint the two
	/// authoritative brief fields — the silhouette occupancy mask and the colour
	/// palette — are a thresholding/quantization problem, not a reasoning one, so
	/// they are extracted here in pure code rather than read by a vision model.
	///
	/// Pixels are in Unity's <c>GetPixels32</c> convention: index <c>y*Width + x</c>
	/// with row 0 at the BOTTOM. Silhouette rows come back image-style (top row
	/// first) to match <see cref="SilhouetteSpec"/>.
	/// </summary>
	public static class ReferenceImageAnalysis
	{
		/// <summary>A decoded image: raw pixels plus dimensions, in GetPixels32 order (row 0 = bottom).</summary>
		public readonly struct Pixels
		{
			public Pixels(Color32[] data, int width, int height)
			{
				Data = data;
				Width = width;
				Height = height;
			}

			public Color32[] Data { get; }
			public int Width { get; }
			public int Height { get; }
		}

		/// <summary>
		/// Per-pixel foreground mask (index <c>y*Width + x</c>). When the image
		/// carries real transparency the alpha channel keys it exactly; otherwise
		/// the background colour is read from the corners and every pixel within
		/// <paramref name="bgTolerance"/> (normalised RGB distance) of it is
		/// background. Tolerance, not exact match, so a gradient/noisy "plain"
		/// background still keys cleanly.
		/// </summary>
		public static bool[] ForegroundMask(Pixels image, float bgTolerance)
		{
			var data = image.Data;
			var mask = new bool[data.Length];

			if (HasTransparency(data))
			{
				for (var i = 0; i < data.Length; i++)
				{
					mask[i] = data[i].a >= 128;
				}

				return mask;
			}

			var background = CornerBackground(image);
			for (var i = 0; i < data.Length; i++)
			{
				mask[i] = Distance(data[i], background) > bgTolerance;
			}

			return mask;
		}

		/// <summary>
		/// One occupancy silhouette for a face. The foreground bounding box is
		/// diced into a <c>cols x rows</c> grid (rows fixed to <paramref name="rows"/>,
		/// cols derived to preserve the box's aspect) and a cell is solid when more
		/// than <paramref name="cellCoverage"/> of its pixels are foreground —
		/// per-cell area coverage anti-aliases cleanly and, because the grid hugs
		/// the bounding box, no margin trim is needed. Returns an empty silhouette
		/// when the image has no foreground.
		/// </summary>
		public static SilhouetteSpec Silhouette(string face, Pixels image, int rows, float cellCoverage, float bgTolerance)
		{
			var mask = ForegroundMask(image, bgTolerance);
			if (!BoundingBox(mask, image.Width, image.Height, out var minX, out var minY, out var maxX, out var maxY))
			{
				return new SilhouetteSpec(face, Vector3Int.zero, Array.Empty<string>());
			}

			var boxW = maxX - minX + 1;
			var boxH = maxY - minY + 1;
			var rowCount = Mathf.Max(1, rows);
			var colCount = Mathf.Max(1, Mathf.RoundToInt((float)boxW * rowCount / boxH));

			var foreground = new int[colCount, rowCount];
			var total = new int[colCount, rowCount];
			for (var y = minY; y <= maxY; y++)
			{
				for (var x = minX; x <= maxX; x++)
				{
					var col = Mathf.Clamp((int)((x - minX) * (long)colCount / boxW), 0, colCount - 1);
					var rowFromBottom = Mathf.Clamp((int)((y - minY) * (long)rowCount / boxH), 0, rowCount - 1);
					var row = rowCount - 1 - rowFromBottom; // silhouette rows are top-first
					total[col, row]++;
					if (mask[y * image.Width + x])
					{
						foreground[col, row]++;
					}
				}
			}

			var rowsOut = new string[rowCount];
			for (var row = 0; row < rowCount; row++)
			{
				var chars = new char[colCount];
				for (var col = 0; col < colCount; col++)
				{
					var solid = total[col, row] > 0 && (float)foreground[col, row] / total[col, row] > cellCoverage;
					chars[col] = solid ? '#' : '.';
				}

				rowsOut[row] = new string(chars);
			}

			return new SilhouetteSpec(face, new Vector3Int(colCount, rowCount, 0), rowsOut);
		}

		/// <summary>
		/// One shared palette across every image, capped at <paramref name="maxColours"/>.
		/// Only INTERIOR foreground pixels (all four neighbours foreground) are
		/// counted, which drops the anti-aliased edge blends that would otherwise
		/// pollute a flat-art palette; colours are then taken most-frequent first,
		/// skipping any within <paramref name="mergeDistance"/> of one already kept
		/// (collapses mild compression noise around a flat fill).
		/// </summary>
		public static IReadOnlyList<PaletteEntry> Palette(
			IEnumerable<Pixels> images, int maxColours, float bgTolerance, float mergeDistance)
		{
			var counts = new Dictionary<int, int>();
			var fallback = new Dictionary<int, int>();
			foreach (var image in images)
			{
				var mask = ForegroundMask(image, bgTolerance);
				for (var y = 0; y < image.Height; y++)
				{
					for (var x = 0; x < image.Width; x++)
					{
						if (!mask[y * image.Width + x])
						{
							continue;
						}

						var key = Pack(image.Data[y * image.Width + x]);
						Increment(fallback, key);
						if (IsInterior(mask, image.Width, image.Height, x, y))
						{
							Increment(counts, key);
						}
					}
				}
			}

			// A subject only a pixel or two thick has no interior; fall back to all
			// of its foreground so it still yields a palette.
			var histogram = counts.Count > 0 ? counts : fallback;

			var picked = new List<Color32>();
			foreach (var (key, _) in histogram.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key))
			{
				var colour = Unpack(key);
				if (picked.All(p => Distance(p, colour) > mergeDistance))
				{
					picked.Add(colour);
					if (picked.Count >= maxColours)
					{
						break;
					}
				}
			}

			return picked.Select((colour, index) => new PaletteEntry(KeyFor(index), colour)).ToList();
		}

		private const string KeyAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

		private static char KeyFor(int index) => KeyAlphabet[index % KeyAlphabet.Length];

		private static bool HasTransparency(Color32[] data) => data.Any(p => p.a < 250);

		/// <summary>Per-channel median of the four corner pixels — resists a single corner the subject happens to touch.</summary>
		private static Color32 CornerBackground(Pixels image)
		{
			var w = image.Width;
			var h = image.Height;
			var corners = new[]
			{
				image.Data[0],
				image.Data[w - 1],
				image.Data[(h - 1) * w],
				image.Data[(h - 1) * w + (w - 1)],
			};

			byte Median(Func<Color32, byte> channel)
			{
				var sorted = corners.Select(channel).OrderBy(v => v).ToArray();
				return (byte)((sorted[1] + sorted[2]) / 2);
			}

			return new Color32(Median(c => c.r), Median(c => c.g), Median(c => c.b), 255);
		}

		private static bool BoundingBox(bool[] mask, int width, int height, out int minX, out int minY, out int maxX, out int maxY)
		{
			minX = width;
			minY = height;
			maxX = -1;
			maxY = -1;
			for (var y = 0; y < height; y++)
			{
				for (var x = 0; x < width; x++)
				{
					if (!mask[y * width + x])
					{
						continue;
					}

					minX = Mathf.Min(minX, x);
					maxX = Mathf.Max(maxX, x);
					minY = Mathf.Min(minY, y);
					maxY = Mathf.Max(maxY, y);
				}
			}

			return maxX >= minX && maxY >= minY;
		}

		private static bool IsInterior(bool[] mask, int width, int height, int x, int y) =>
			x > 0 && x < width - 1 && y > 0 && y < height - 1 &&
			mask[y * width + (x - 1)] && mask[y * width + (x + 1)] &&
			mask[(y - 1) * width + x] && mask[(y + 1) * width + x];

		private static void Increment(IDictionary<int, int> counts, int key) =>
			counts[key] = counts.TryGetValue(key, out var n) ? n + 1 : 1;

		private static int Pack(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;

		private static Color32 Unpack(int key) =>
			new((byte)((key >> 16) & 0xff), (byte)((key >> 8) & 0xff), (byte)(key & 0xff), 255);

		/// <summary>Euclidean RGB distance normalised to 0..1 (1 = black-to-white).</summary>
		private static float Distance(Color32 a, Color32 b)
		{
			float dr = a.r - b.r;
			float dg = a.g - b.g;
			float db = a.b - b.b;
			return Mathf.Sqrt(dr * dr + dg * dg + db * db) / (255f * Mathf.Sqrt(3f));
		}
	}
}
