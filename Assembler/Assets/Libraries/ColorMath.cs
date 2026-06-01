using UnityEngine;

namespace Assembler.Libraries
{
	/// <summary>
	/// First-class colour helpers for descriptor expressions. Registered globally in
	/// CompiledExpressionsRegistry so every expression can call these by bare name
	/// (LerpColor, WithAlpha, Brighten, RgbToHsv, ...). Colours are UnityEngine.Color
	/// (already registered as a constructible type). HSV triples are carried as
	/// Vector3(h, s, v) with all components in [0, 1]. Numeric parameters are float so int
	/// arguments coerce automatically during overload resolution.
	/// </summary>
	public static class ColorMath
	{
		/// <summary>Linear interpolation between two colours (t clamped to [0, 1]).</summary>
		/// <param name="a">Start colour (t = 0).</param>
		/// <param name="b">End colour (t = 1).</param>
		/// <param name="t">Interpolation factor; clamped to [0, 1].</param>
		/// <returns>The interpolated colour.</returns>
		public static Color LerpColor(Color a, Color b, float t) => Color.Lerp(a, b, t);

		/// <summary>The same colour with its alpha replaced.</summary>
		/// <param name="c">The colour.</param>
		/// <param name="alpha">New alpha in [0, 1].</param>
		/// <returns>The colour with the given alpha.</returns>
		public static Color WithAlpha(Color c, float alpha) => new(c.r, c.g, c.b, alpha);

		/// <summary>Brighten a colour by scaling its RGB toward white (alpha preserved).</summary>
		/// <param name="c">The colour.</param>
		/// <param name="factor">Brighten amount in [0, 1]; 0 leaves the colour unchanged, 1 is white.</param>
		/// <returns>The brightened colour.</returns>
		public static Color Brighten(Color c, float factor) => new(
			Mathf.Lerp(c.r, 1f, factor),
			Mathf.Lerp(c.g, 1f, factor),
			Mathf.Lerp(c.b, 1f, factor),
			c.a);

		/// <summary>Darken a colour by scaling its RGB toward black (alpha preserved).</summary>
		/// <param name="c">The colour.</param>
		/// <param name="factor">Darken amount in [0, 1]; 0 leaves the colour unchanged, 1 is black.</param>
		/// <returns>The darkened colour.</returns>
		public static Color Darken(Color c, float factor) => new(
			Mathf.Lerp(c.r, 0f, factor),
			Mathf.Lerp(c.g, 0f, factor),
			Mathf.Lerp(c.b, 0f, factor),
			c.a);

		/// <summary>A grayscale colour matching the perceived luminance of the input (alpha preserved).</summary>
		/// <param name="c">The colour.</param>
		/// <returns>The desaturated grayscale colour.</returns>
		public static Color Grayscale(Color c)
		{
			float l = c.grayscale;
			return new Color(l, l, l, c.a);
		}

		/// <summary>Convert an RGB colour to HSV as Vector3(h, s, v), each in [0, 1].</summary>
		/// <param name="c">The colour to convert.</param>
		/// <returns>The hue, saturation and value packed as a Vector3.</returns>
		public static Vector3 RgbToHsv(Color c)
		{
			Color.RGBToHSV(c, out float h, out float s, out float v);
			return new Vector3(h, s, v);
		}

		/// <summary>Build an opaque colour from hue, saturation and value (each in [0, 1]).</summary>
		/// <param name="h">Hue in [0, 1].</param>
		/// <param name="s">Saturation in [0, 1].</param>
		/// <param name="v">Value in [0, 1].</param>
		/// <returns>The opaque RGB colour.</returns>
		public static Color HsvToRgb(float h, float s, float v) => Color.HSVToRGB(h, s, v);
	}
}
