using System;
using System.Globalization;
using UnityEngine;

namespace Assembler.Voxelization
{
	/// <summary>
	/// One named colour in a model's palette. The single-character key is what
	/// layer ASCII cells reference; the reserved key '_' (and '.' in layer cells)
	/// means empty and never appears as an entry. Entry order is significant:
	/// entry i corresponds to 1-based palette index i+1 in every part grid.
	/// </summary>
	public sealed record PaletteEntry(char Key, Color32 Colour)
	{
		public const char EmptyKey = '_';
		public const char EmptyCell = '.';

		public string ToHex() =>
			Colour.a == 255
				? $"#{Colour.r:x2}{Colour.g:x2}{Colour.b:x2}"
				: $"#{Colour.r:x2}{Colour.g:x2}{Colour.b:x2}{Colour.a:x2}";

		public static Color32 ParseHex(string hex)
		{
			var s = hex.Trim();
			if (s.StartsWith("#", StringComparison.Ordinal))
			{
				s = s.Substring(1);
			}

			if (s.Length != 6 && s.Length != 8)
			{
				throw new FormatException($"Colour '{hex}' is not a #RRGGBB or #RRGGBBAA hex value.");
			}

			byte Part(int offset) => byte.Parse(s.Substring(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			return new Color32(Part(0), Part(2), Part(4), s.Length == 8 ? Part(6) : (byte)255);
		}
	}
}
