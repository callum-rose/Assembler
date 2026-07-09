namespace Assembler.Time
{
	/// <summary>
	/// Per-run configuration threaded into the builder's instantiate phase: which <see cref="IGameClock"/> to
	/// run on and the PRNG seed. Together these are the two foundational determinism sources (issue #101), and
	/// this record is the container a future replay header maps onto (clock mode + seed + input log).
	/// </summary>
	/// <param name="ClockMode">
	/// Which clock to run on. Defaults to <see cref="GameClockMode.Realtime"/> so normal play and the sandbox
	/// validator are unchanged; deterministic / replay runs pass <see cref="GameClockMode.FixedStep"/>.
	/// </param>
	/// <param name="Seed">
	/// The run seed for the ambient PRNG. <c>null</c> means "derive a fresh seed from entropy at run start" —
	/// so normal play still varies each launch while remaining capturable; an explicit seed makes the run's
	/// randomness reproducible.
	/// </param>
	public sealed record RunOptions(GameClockMode ClockMode = GameClockMode.Realtime, uint? Seed = null)
	{
		/// <summary>The default run: realtime clock, entropy-seeded RNG. Matches pre-seed behaviour.</summary>
		public static readonly RunOptions Default = new();
	}
}
