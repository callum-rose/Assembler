using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Libraries
{
	/// <summary>
	/// First-class randomness helpers for descriptor expressions. Registered globally in
	/// CompiledExpressionsRegistry so every expression can call these by bare name (RandomFloat,
	/// RandomOnCircle, RandomColor, ...). All numeric parameters are float so int arguments coerce
	/// automatically during overload resolution. Lists are carried as List&lt;T&gt;, matching GridMath.
	///
	/// Routes through the seeded <see cref="RandomState.Current"/> rather than UnityEngine.Random so runs are
	/// deterministic given a seed (Level 1 determinism; see CLAUDE.md). Signatures are unchanged from the
	/// UnityEngine.Random version so the compiler registration and generated Libraries.md are unaffected.
	/// </summary>
	public static class RandomMath
	{
		/// <summary>A random float in the inclusive range [min, max].</summary>
		/// <param name="min">Lower bound (inclusive).</param>
		/// <param name="max">Upper bound (inclusive).</param>
		/// <returns>A uniformly random float in the range.</returns>
		public static float RandomFloat(float min, float max) => RandomState.Current.NextFloat(min, max);

		/// <summary>A random integer in the inclusive range [minInclusive, maxInclusive].</summary>
		/// <param name="minInclusive">Lower bound (inclusive).</param>
		/// <param name="maxInclusive">Upper bound (inclusive).</param>
		/// <returns>A uniformly random integer in the range.</returns>
		public static int RandomInt(float minInclusive, float maxInclusive) =>
			RandomState.Current.NextInt((int)minInclusive, (int)maxInclusive + 1);

		/// <summary>True with the given probability.</summary>
		/// <param name="probability">Chance of returning true, in [0, 1].</param>
		/// <returns>A random boolean weighted by <paramref name="probability"/>.</returns>
		public static bool Chance(float probability) => RandomState.Current.Value < probability;

		/// <summary>A random point on the circumference of a circle of the given radius (z = 0).</summary>
		/// <param name="radius">The circle radius.</param>
		/// <returns>A random Vector3 on the circle, in the XY plane.</returns>
		public static Vector3 RandomOnCircle(float radius)
		{
			float angle = RandomState.Current.NextFloat(0f, Mathf.PI * 2f);
			return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
		}

		/// <summary>A random point inside a disc of the given radius (z = 0).</summary>
		/// <param name="radius">The disc radius.</param>
		/// <returns>A random Vector3 inside the disc, in the XY plane.</returns>
		public static Vector3 RandomInsideCircle(float radius)
		{
			// Uniform over the disc: sqrt-distributed radius keeps density constant with area.
			float angle = RandomState.Current.NextFloat(0f, Mathf.PI * 2f);
			float r = Mathf.Sqrt(RandomState.Current.Value) * radius;
			return new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f);
		}

		/// <summary>A random fully-opaque RGB colour.</summary>
		/// <returns>A random opaque Color.</returns>
		public static Color RandomColor()
		{
			float r = RandomState.Current.Value;
			float g = RandomState.Current.Value;
			float b = RandomState.Current.Value;
			return new Color(r, g, b, 1f);
		}

		/// <summary>A random opaque colour with each channel between the matching channels of two colours.</summary>
		/// <param name="a">One end of the per-channel range.</param>
		/// <param name="b">The other end of the per-channel range.</param>
		/// <returns>A random opaque Color blended per channel between a and b.</returns>
		public static Color RandomColorBetween(Color a, Color b)
		{
			float r = RandomState.Current.NextFloat(a.r, b.r);
			float g = RandomState.Current.NextFloat(a.g, b.g);
			float bl = RandomState.Current.NextFloat(a.b, b.b);
			return new Color(r, g, bl, 1f);
		}

		/// <summary>A random element from a list of vectors.</summary>
		/// <param name="items">The list to pick from (must be non-empty).</param>
		/// <returns>A uniformly random element.</returns>
		public static Vector3 Pick(List<Vector3> items) => items[RandomState.Current.NextInt(0, items.Count)];

		/// <summary>A random element from a list of integers.</summary>
		/// <param name="items">The list to pick from (must be non-empty).</param>
		/// <returns>A uniformly random element.</returns>
		public static int PickInt(List<int> items) => items[RandomState.Current.NextInt(0, items.Count)];
	}
}
