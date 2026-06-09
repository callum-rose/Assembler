using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Assembler.Voxels.Generation
{
	/// <summary>
	/// Reduces an image's colours to a small representative palette via
	/// deterministic k-means (Lloyd's algorithm). Used to seed Claude with the
	/// reference image's actual colours so generated models pick up its palette
	/// instead of inventing flat defaults. Pure and CPU-only — decoding the PNG
	/// into pixels is the caller's job (see <see cref="ImagePalette"/>).
	/// </summary>
	public static class PaletteQuantizer
	{
		/// <summary>
		/// Quantises <paramref name="pixels"/> to at most <paramref name="maxColors"/>
		/// representative colours. Fully opaque output. Deterministic: centroid
		/// seeding is frequency-based, so the same pixels always yield the same
		/// palette. Returns colours ordered by descending cluster weight.
		/// </summary>
		public static Color32[] Quantize(IReadOnlyList<Color32> pixels, int maxColors, int iterations = 8)
		{
			if (pixels == null || pixels.Count == 0 || maxColors <= 0)
			{
				return Array.Empty<Color32>();
			}

			// Collapse to distinct colours with weights — skip near-transparent
			// pixels (background) so they don't dominate the palette.
			var weights = new Dictionary<int, int>();
			foreach (var p in pixels)
			{
				if (p.a < 16)
				{
					continue;
				}

				var key = (p.r << 16) | (p.g << 8) | p.b;
				weights[key] = weights.TryGetValue(key, out var w) ? w + 1 : 1;
			}

			if (weights.Count == 0)
			{
				return Array.Empty<Color32>();
			}

			var distinct = new List<(Vector3 rgb, int weight)>(weights.Count);
			foreach (var kv in weights)
			{
				var r = (kv.Key >> 16) & 0xFF;
				var g = (kv.Key >> 8) & 0xFF;
				var b = kv.Key & 0xFF;
				distinct.Add((new Vector3(r, g, b), kv.Value));
			}

			var k = Mathf.Min(maxColors, distinct.Count);

			// Seed centroids with the k heaviest distinct colours (deterministic).
			distinct.Sort((a, b) => b.weight.CompareTo(a.weight));
			var centroids = new Vector3[k];
			for (var i = 0; i < k; i++)
			{
				centroids[i] = distinct[i].rgb;
			}

			var assignment = new int[distinct.Count];
			for (var iter = 0; iter < iterations; iter++)
			{
				var changed = false;
				for (var i = 0; i < distinct.Count; i++)
				{
					var best = 0;
					var bestDist = float.MaxValue;
					for (var c = 0; c < k; c++)
					{
						var d = (distinct[i].rgb - centroids[c]).sqrMagnitude;
						if (d < bestDist)
						{
							bestDist = d;
							best = c;
						}
					}

					if (assignment[i] != best)
					{
						assignment[i] = best;
						changed = true;
					}
				}

				var sums = new Vector3[k];
				var counts = new long[k];
				for (var i = 0; i < distinct.Count; i++)
				{
					var c = assignment[i];
					sums[c] += distinct[i].rgb * distinct[i].weight;
					counts[c] += distinct[i].weight;
				}

				for (var c = 0; c < k; c++)
				{
					if (counts[c] > 0)
					{
						centroids[c] = sums[c] / counts[c];
					}
				}

				if (!changed && iter > 0)
				{
					break;
				}
			}

			// Order by descending cluster weight so the dominant colours come first.
			var clusterWeight = new long[k];
			for (var i = 0; i < distinct.Count; i++)
			{
				clusterWeight[assignment[i]] += distinct[i].weight;
			}

			var order = new int[k];
			for (var i = 0; i < k; i++)
			{
				order[i] = i;
			}

			Array.Sort(order, (a, b) => clusterWeight[b].CompareTo(clusterWeight[a]));

			var result = new Color32[k];
			for (var i = 0; i < k; i++)
			{
				var c = centroids[order[i]];
				result[i] = new Color32(ClampByte(c.x), ClampByte(c.y), ClampByte(c.z), 255);
			}

			return result;
		}

		/// <summary>Formats a palette as a space-separated "RRGGBB" hex list.</summary>
		public static string ToHexList(IReadOnlyList<Color32> palette)
		{
			var sb = new StringBuilder();
			for (var i = 0; i < palette.Count; i++)
			{
				if (i > 0)
				{
					sb.Append(' ');
				}

				var c = palette[i];
				sb.Append(c.r.ToString("x2", CultureInfo.InvariantCulture))
					.Append(c.g.ToString("x2", CultureInfo.InvariantCulture))
					.Append(c.b.ToString("x2", CultureInfo.InvariantCulture));
			}

			return sb.ToString();
		}

		private static byte ClampByte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v), 0, 255);
	}
}
