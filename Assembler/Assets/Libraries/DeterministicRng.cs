namespace Assembler.Libraries
{
	/// <summary>
	/// A small deterministic pseudo-random generator (PCG32) seeded by a single <see cref="uint"/>. Two
	/// generators built from the same seed produce an identical draw sequence on any build/machine, so any
	/// randomness routed through one replays exactly — the RNG half of the Level-1 determinism goal (issue
	/// #101). Self-contained (no <c>UnityEngine.Random</c>, no <c>Unity.Mathematics</c> dependency) so
	/// <see cref="Assembler.Libraries"/> keeps its single reference on <c>Assembler.Core</c>.
	/// </summary>
	/// <remarks>
	/// A mutable value type: each draw advances <see cref="_state"/>. Stored in a (non-<c>readonly</c>) field —
	/// e.g. <see cref="RandomMath"/>'s ambient generator — a draw mutates that field in place. Copying the
	/// struct forks the sequence, which is deliberately how independent streams would be spun off later.
	/// PCG32 is the "PCG-XSH-RR 64/32" variant (O'Neill 2014): a 64-bit LCG whose state is permuted down to a
	/// well-distributed 32-bit output. Chosen over a bare xorshift for far better statistical quality at a
	/// near-identical cost, and over <c>Unity.Mathematics.Random</c> to avoid an asmdef dependency.
	/// </remarks>
	public struct DeterministicRng
	{
		// LCG multiplier from the reference PCG implementation; the increment must be odd (see the ctor).
		private const ulong Multiplier = 6364136223846793005UL;

		// Fixed stream selector, so a given seed always maps to the same sequence. A different constant here
		// would yield a different (still deterministic) stream for the same seed.
		private const ulong DefaultStream = 0xda3e39cb94b95bdbUL;

		// 1 / 2^24 — scales a 24-bit draw into [0, 1), matching a float's 24-bit mantissa (no rounding to 1f).
		private const float FloatUnit = 1f / 16777216f;

		private ulong _state;
		private ulong _inc;

		/// <summary>Builds a generator for <paramref name="seed"/> on the default stream.</summary>
		public DeterministicRng(uint seed) : this(seed, DefaultStream)
		{
		}

		/// <summary>
		/// Builds a generator for <paramref name="seed"/> on an explicit <paramref name="stream"/>. Two
		/// generators sharing a seed but differing in stream produce distinct, non-overlapping sequences.
		/// </summary>
		public DeterministicRng(uint seed, ulong stream)
		{
			_state = 0UL;
			_inc = (stream << 1) | 1UL; // force odd, as PCG requires
			NextUInt();
			unchecked
			{
				_state += seed;
			}

			NextUInt();
		}

		/// <summary>Advances the generator and returns the next 32-bit value, uniform over the whole range.</summary>
		public uint NextUInt()
		{
			unchecked
			{
				var previous = _state;
				_state = previous * Multiplier + _inc;

				var xorshifted = (uint)(((previous >> 18) ^ previous) >> 27);
				var rot = (int)(previous >> 59);
				return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
			}
		}

		/// <summary>The next float in the half-open range [0, 1).</summary>
		public float NextFloat() => (NextUInt() >> 8) * FloatUnit;

		/// <summary>The next float in [min, max) (or exactly <paramref name="min"/> when min ≥ max).</summary>
		public float NextFloat(float min, float max) => min + NextFloat() * (max - min);

		/// <summary>
		/// The next int in the half-open range [minInclusive, maxExclusive), matching
		/// <c>UnityEngine.Random.Range(int, int)</c>. Returns <paramref name="minInclusive"/> for an empty range.
		/// </summary>
		public int NextInt(int minInclusive, int maxExclusive)
		{
			if (maxExclusive <= minInclusive)
			{
				return minInclusive;
			}

			var range = (uint)(maxExclusive - minInclusive);
			return minInclusive + (int)(NextUInt() % range);
		}
	}
}
