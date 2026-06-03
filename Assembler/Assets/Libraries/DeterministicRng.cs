namespace Assembler.Libraries
{
	/// <summary>
	/// A pure-C# seeded pseudo-random number generator (PCG32) used for the Level 1 determinism guarantee.
	/// Independent of <c>UnityEngine.Random</c>'s global state, so a run's randomness is fully captured by its
	/// seed and reproducible on the same build/machine. Single-threaded; intended for the Unity main thread.
	/// </summary>
	public sealed class DeterministicRng
	{
		private const ulong Multiplier = 6364136223846793005UL;
		// A fixed stream selector. Combined with the seed it fully determines the sequence.
		private const ulong DefaultStream = 1442695040888963407UL;

		private ulong _state;
		private readonly ulong _inc;

		public DeterministicRng(uint seed)
		{
			_inc = (DefaultStream << 1) | 1UL;
			_state = 0UL;
			NextUInt();
			_state += seed;
			NextUInt();
		}

		/// <summary>The current internal generator state. Lets a run snapshot/restore its RNG position.</summary>
		public ulong State => _state;

		/// <summary>A uniformly random uint across the full 32-bit range.</summary>
		public uint NextUInt()
		{
			var oldState = _state;
			_state = oldState * Multiplier + _inc;
			var xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
			var rot = (int)(oldState >> 59);
			return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
		}

		/// <summary>A uniformly random float in [0, 1) with 24 bits of precision.</summary>
		public float Value => (NextUInt() >> 8) * (1f / 16777216f);

		/// <summary>A uniformly random float in [min, max).</summary>
		public float NextFloat(float min, float max) => min + Value * (max - min);

		/// <summary>A uniformly random int in [minInclusive, maxExclusive).</summary>
		public int NextInt(int minInclusive, int maxExclusive)
		{
			if (maxExclusive <= minInclusive) return minInclusive;
			var range = (uint)(maxExclusive - minInclusive);
			return minInclusive + (int)(NextUInt() % range);
		}
	}

	/// <summary>
	/// Ambient holder for the single per-run <see cref="DeterministicRng"/>. <see cref="RandomMath"/> delegates to
	/// <see cref="Current"/>, and the builder calls <see cref="Seed"/> once at startup with the run's seed. Single-threaded
	/// (Unity main thread); see the Determinism (Level 1) section in CLAUDE.md.
	/// </summary>
	public static class RandomState
	{
		/// <summary>The active per-run generator. Replaced by <see cref="Seed"/>; never null.</summary>
		public static DeterministicRng Current { get; private set; } = new(0);

		/// <summary>Reseeds the ambient generator, starting a fresh deterministic sequence.</summary>
		public static void Seed(uint seed) => Current = new DeterministicRng(seed);
	}
}
