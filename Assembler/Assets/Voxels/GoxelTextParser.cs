using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Assembler.Voxels
{
	/// <summary>
	/// Parses Goxel's plain-text export format: one voxel per line in the form
	/// "x y z RRGGBB" (whitespace-separated, hex color without leading #).
	/// Lines that are blank or start with '#' are ignored.
	/// </summary>
	public static class GoxelTextParser
	{
		public static VoxelModel Parse(string text)
		{
			var voxels = new Dictionary<Vector3Int, byte>();
			var paletteIndex = new Dictionary<Color32, byte>(new Color32Comparer());
			var palette = new List<Color32>();

			Vector3Int min = new(int.MaxValue, int.MaxValue, int.MaxValue);
			Vector3Int max = new(int.MinValue, int.MinValue, int.MinValue);
			var hasAny = false;

			var lines = text.Split('\n');
			foreach (var rawLine in lines)
			{
				var line = rawLine.Trim();
				if (line.Length == 0 || line[0] == '#')
				{
					continue;
				}

				var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length < 4)
				{
					continue;
				}

				if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
					!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) ||
					!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var z))
				{
					continue;
				}

				if (!TryParseHexColor(parts[3], out var colour))
				{
					continue;
				}

				if (!paletteIndex.TryGetValue(colour, out var index))
				{
					if (palette.Count >= 255)
					{
						throw new InvalidOperationException(
							"Goxel text contains more than 255 distinct colours, which exceeds the .vox palette limit.");
					}

					index = (byte)(palette.Count + 1);
					palette.Add(colour);
					paletteIndex[colour] = index;
				}

				var pos = new Vector3Int(x, y, z);
				voxels[pos] = index;

				if (!hasAny)
				{
					min = max = pos;
					hasAny = true;
				}
				else
				{
					min = Vector3Int.Min(min, pos);
					max = Vector3Int.Max(max, pos);
				}
			}

			if (!hasAny)
			{
				min = max = Vector3Int.zero;
			}

			return new VoxelModel(voxels, palette.ToArray(), min, max);
		}

		private static bool TryParseHexColor(string hex, out Color32 colour)
		{
			colour = default;
			if (hex.Length >= 1 && hex[0] == '#')
			{
				hex = hex.Substring(1);
			}

			if (hex.Length == 6)
			{
				if (TryHexByte(hex, 0, out var r) && TryHexByte(hex, 2, out var g) && TryHexByte(hex, 4, out var b))
				{
					colour = new Color32(r, g, b, 255);
					return true;
				}
			}
			else if (hex.Length == 8)
			{
				if (TryHexByte(hex, 0, out var r) && TryHexByte(hex, 2, out var g) &&
					TryHexByte(hex, 4, out var b) && TryHexByte(hex, 6, out var a))
				{
					colour = new Color32(r, g, b, a);
					return true;
				}
			}

			return false;
		}

		private static bool TryHexByte(string s, int offset, out byte value)
		{
			return byte.TryParse(s.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
		}

		private sealed class Color32Comparer : IEqualityComparer<Color32>
		{
			public bool Equals(Color32 x, Color32 y) => x.r == y.r && x.g == y.g && x.b == y.b && x.a == y.a;
			public int GetHashCode(Color32 c) => (c.r << 24) | (c.g << 16) | (c.b << 8) | c.a;
		}
	}
}
