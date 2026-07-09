namespace Assembler.Time
{
	/// <summary>
	/// Selects which <see cref="IGameClock"/> a game runs on. <see cref="Realtime"/> is the default for every
	/// normal play session; <see cref="FixedStep"/> is chosen only for deterministic / replay runs, where a
	/// constant per-frame delta is required so the same descriptor replays identically (Level 1: same build,
	/// same machine; physics-driven games excluded).
	/// </summary>
	public enum GameClockMode
	{
		/// <summary>Wall-clock driven (<see cref="RealtimeGameClock"/>). The default.</summary>
		Realtime,

		/// <summary>Constant fixed delta per frame (<see cref="FixedStepGameClock"/>), for deterministic runs.</summary>
		FixedStep
	}
}
