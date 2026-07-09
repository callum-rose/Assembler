using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Libraries
{
	/// <summary>
	/// First-class randomness helpers for descriptor expressions, registered globally in
	/// CompiledExpressionsRegistry so every expression can call these by bare name (RandomFloat,
	/// RandomOnCircle, RandomColor, ...). All numeric parameters are float so int arguments coerce
	/// automatically during overload resolution. Lists are carried as List&lt;T&gt;, matching GridMath.
	/// </summary>
	/// <remarks>
	/// Every helper draws from a single ambient <see cref="DeterministicRng"/> — the per-run seeded PRNG
	/// (issue #101) — rather than <c>UnityEngine.Random</c>, so a game's randomness replays exactly given the
	/// same seed (Level 1: same build, same machine). <see cref="Builder"/> calls <see cref="Seed(uint)"/>
	/// once at run start (an explicit seed for a deterministic/replay run, otherwise one derived from entropy
	/// so normal play still varies each launch). The generator is static, so it assumes one game runs at a
	/// time — fine at Level 1. Anything that calls <c>UnityEngine.Random</c> directly (including a descriptor
	/// expression that names <c>UnityEngine.Random.Range</c> by qualified name) bypasses this seed and will
	/// not replay.
	/// </remarks>
	public static class RandomMath
	{
		// The single source of randomness for every helper below. Seeded from entropy on first use so a
		// process that never calls Seed still varies; Builder overwrites it per run via Seed(uint).
		private static DeterministicRng _rng = new(unchecked((uint)System.Environment.TickCount));

		/// <summary>
		/// Reseeds the ambient generator so every subsequent draw follows a fresh, reproducible sequence.
		/// Called once per run by the builder; the same seed yields the same game randomness.
		/// </summary>
		/// <param name="seed">The run seed.</param>
		public static void Seed(uint seed) => _rng = new DeterministicRng(seed);

		/// <summary>A random float in the range [min, max).</summary>
		/// <param name="min">Lower bound (inclusive).</param>
		/// <param name="max">Upper bound (exclusive).</param>
		/// <returns>A uniformly random float in the range.</returns>
		public static float RandomFloat(float min, float max) => _rng.NextFloat(min, max);

		/// <summary>A random integer in the inclusive range [minInclusive, maxInclusive].</summary>
		/// <param name="minInclusive">Lower bound (inclusive).</param>
		/// <param name="maxInclusive">Upper bound (inclusive).</param>
		/// <returns>A uniformly random integer in the range.</returns>
		public static int RandomInt(float minInclusive, float maxInclusive) =>
			_rng.NextInt((int)minInclusive, (int)maxInclusive + 1);

		/// <summary>True with the given probability.</summary>
		/// <param name="probability">Chance of returning true, in [0, 1].</param>
		/// <returns>A random boolean weighted by <paramref name="probability"/>.</returns>
		public static bool Chance(float probability) => _rng.NextFloat() < probability;

		/// <summary>A random point on the circumference of a circle of the given radius (z = 0).</summary>
		/// <param name="radius">The circle radius.</param>
		/// <returns>A random Vector3 on the circle, in the XY plane.</returns>
		public static Vector3 RandomOnCircle(float radius)
		{
			float angle = _rng.NextFloat(0f, Mathf.PI * 2f);
			return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
		}

		/// <summary>A random point inside a disc of the given radius (z = 0).</summary>
		/// <param name="radius">The disc radius.</param>
		/// <returns>A random Vector3 inside the disc, in the XY plane.</returns>
		public static Vector3 RandomInsideCircle(float radius)
		{
			// Uniform over area: the radius scales with sqrt of a uniform draw (a linear radius would bunch
			// points near the centre). Angle is a second independent uniform draw.
			float r = Mathf.Sqrt(_rng.NextFloat()) * radius;
			float angle = _rng.NextFloat(0f, Mathf.PI * 2f);
			return new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f);
		}

		/// <summary>A random fully-opaque RGB colour.</summary>
		/// <returns>A random opaque Color.</returns>
		public static Color RandomColor() => new(_rng.NextFloat(), _rng.NextFloat(), _rng.NextFloat(), 1f);

		/// <summary>A random opaque colour with each channel between the matching channels of two colours.</summary>
		/// <param name="a">One end of the per-channel range.</param>
		/// <param name="b">The other end of the per-channel range.</param>
		/// <returns>A random opaque Color blended per channel between a and b.</returns>
		public static Color RandomColorBetween(Color a, Color b) => new(
			_rng.NextFloat(a.r, b.r),
			_rng.NextFloat(a.g, b.g),
			_rng.NextFloat(a.b, b.b),
			1f);

		/// <summary>A random element from a list of vectors.</summary>
		/// <param name="items">The list to pick from (must be non-empty).</param>
		/// <returns>A uniformly random element.</returns>
		public static Vector3 Pick(List<Vector3> items) => items[_rng.NextInt(0, items.Count)];

		/// <summary>A random element from a list of integers.</summary>
		/// <param name="items">The list to pick from (must be non-empty).</param>
		/// <returns>A uniformly random element.</returns>
		public static int PickInt(List<int> items) => items[_rng.NextInt(0, items.Count)];

		/// <summary>An index in [0, weights.Count) chosen with probability proportional to each weight.</summary>
		/// <param name="weights">Per-item weights; negatives are clamped to 0. An all-zero list falls back to a uniform pick.</param>
		/// <returns>A weighted-random index into the list (must be non-empty).</returns>
		public static int WeightedPickIndex(List<float> weights)
		{
			float total = 0f;
			foreach (var weight in weights)
			{
				total += Mathf.Max(0f, weight);
			}

			if (total <= 0f)
			{
				return _rng.NextInt(0, weights.Count);
			}

			float roll = _rng.NextFloat() * total;
			for (int i = 0; i < weights.Count; i++)
			{
				roll -= Mathf.Max(0f, weights[i]);
				if (roll < 0f)
				{
					return i;
				}
			}

			return weights.Count - 1;
		}
	}
}
