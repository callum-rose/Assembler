using UnityEngine;

namespace Assembler.Libraries
{
	/// <summary>
	/// First-class scalar math helpers for descriptor expressions. Registered globally in
	/// CompiledExpressionsRegistry so every expression can call these by bare name (Clamp,
	/// Lerp, Remap, DegToRad, ...). All numeric parameters are float so int arguments coerce
	/// automatically during overload resolution. Names are scalar-specific (Lerp here,
	/// LerpVector in VectorMath, LerpColor in ColorMath) to keep the shared bare-name space
	/// unambiguous.
	/// </summary>
	public static class NumberMath
	{
		/// <summary>Constrain a value to the inclusive range [min, max].</summary>
		/// <param name="x">The value to clamp.</param>
		/// <param name="min">Lower bound.</param>
		/// <param name="max">Upper bound.</param>
		/// <returns>The clamped value.</returns>
		public static float Clamp(float x, float min, float max) => Mathf.Clamp(x, min, max);

		/// <summary>Constrain a value to [0, 1].</summary>
		/// <param name="x">The value to clamp.</param>
		/// <returns>The value clamped to [0, 1].</returns>
		public static float Clamp01(float x) => Mathf.Clamp01(x);

		/// <summary>The smaller of two values.</summary>
		/// <param name="a">First value.</param>
		/// <param name="b">Second value.</param>
		/// <returns>The minimum of a and b.</returns>
		public static float Min(float a, float b) => Mathf.Min(a, b);

		/// <summary>The larger of two values.</summary>
		/// <param name="a">First value.</param>
		/// <param name="b">Second value.</param>
		/// <returns>The maximum of a and b.</returns>
		public static float Max(float a, float b) => Mathf.Max(a, b);

		/// <summary>Absolute value.</summary>
		/// <param name="x">The value.</param>
		/// <returns>The magnitude of x with the sign removed.</returns>
		public static float Abs(float x) => Mathf.Abs(x);

		/// <summary>Sign of a value: -1, 0, or 1.</summary>
		/// <param name="x">The value.</param>
		/// <returns>-1 if negative, 1 if positive, 0 if zero.</returns>
		public static float Sign(float x) => x > 0f ? 1f : x < 0f ? -1f : 0f;

		/// <summary>Round to the nearest whole number (banker's rounding at .5).</summary>
		/// <param name="x">The value to round.</param>
		/// <returns>The nearest integral value.</returns>
		public static float Round(float x) => Mathf.Round(x);

		/// <summary>Largest whole number less than or equal to a value.</summary>
		/// <param name="x">The value.</param>
		/// <returns>The floor of x.</returns>
		public static float Floor(float x) => Mathf.Floor(x);

		/// <summary>Smallest whole number greater than or equal to a value.</summary>
		/// <param name="x">The value.</param>
		/// <returns>The ceiling of x.</returns>
		public static float Ceil(float x) => Mathf.Ceil(x);

		/// <summary>Linear interpolation between two values (t clamped to [0, 1]).</summary>
		/// <param name="a">Start value (t = 0).</param>
		/// <param name="b">End value (t = 1).</param>
		/// <param name="t">Interpolation factor; clamped to [0, 1].</param>
		/// <returns>The interpolated value.</returns>
		public static float Lerp(float a, float b, float t) => Mathf.Lerp(a, b, t);

		/// <summary>
		/// Re-map a value from one range to another (linear). A value at inMin maps to
		/// outMin and a value at inMax maps to outMax; values outside [inMin, inMax] are
		/// extrapolated.
		/// </summary>
		/// <param name="x">The value to re-map.</param>
		/// <param name="inMin">Lower bound of the input range.</param>
		/// <param name="inMax">Upper bound of the input range.</param>
		/// <param name="outMin">Lower bound of the output range.</param>
		/// <param name="outMax">Upper bound of the output range.</param>
		/// <returns>The re-mapped value.</returns>
		public static float Remap(float x, float inMin, float inMax, float outMin, float outMax) =>
			outMin + (x - inMin) * (outMax - outMin) / (inMax - inMin);

		/// <summary>Convert degrees to radians.</summary>
		/// <param name="degrees">An angle in degrees.</param>
		/// <returns>The angle in radians.</returns>
		public static float DegToRad(float degrees) => degrees * Mathf.Deg2Rad;

		/// <summary>Convert radians to degrees.</summary>
		/// <param name="radians">An angle in radians.</param>
		/// <returns>The angle in degrees.</returns>
		public static float RadToDeg(float radians) => radians * Mathf.Rad2Deg;

		/// <summary>True when two values are equal within floating-point tolerance.</summary>
		/// <param name="a">First value.</param>
		/// <param name="b">Second value.</param>
		/// <returns>Whether a and b are approximately equal.</returns>
		public static bool Approx(float a, float b) => Mathf.Approximately(a, b);
	}
}
