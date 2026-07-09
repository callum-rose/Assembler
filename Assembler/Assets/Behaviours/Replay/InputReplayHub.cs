namespace Assembler.Behaviours.Replay
{
	/// <summary>
	/// The ambient pointer to the active <see cref="InputReplaySession"/> for the current run, or <c>null</c> in
	/// normal play. Input triggers read it to record their emissions and to know when to suppress live device
	/// reads, without every trigger having to be injected with the session.
	/// </summary>
	/// <remarks>
	/// Static-ambient by design, mirroring the seeded <c>RandomMath</c>: the determinism guarantee is Level 1 (one
	/// game per process at a time), so a single ambient session is sufficient and keeps the wiring out of the
	/// per-behaviour build path. The run's <see cref="ReplayDriver"/> publishes the session for the game's lifetime
	/// and clears it on teardown, so it never leaks into a subsequent game loaded in the same process.
	/// </remarks>
	public static class InputReplayHub
	{
		public static InputReplaySession? Current { get; set; }
	}
}
