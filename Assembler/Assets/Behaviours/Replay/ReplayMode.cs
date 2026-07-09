namespace Assembler.Behaviours.Replay
{
	/// <summary>
	/// Whether the current run is capturing input, replaying a captured log, or neither. Carried on
	/// <see cref="InputReplaySession"/> and read by the input triggers (via <see cref="InputReplayHub"/>) to
	/// decide whether to record their emissions and whether to read the live device at all.
	/// </summary>
	public enum ReplayMode
	{
		/// <summary>Normal play — no session is active (this is the ambient hub's null state), input is neither recorded nor replayed.</summary>
		Off,

		/// <summary>Live input drives the game as usual, and every input-trigger emission is appended to the log.</summary>
		Record,

		/// <summary>Live device reads are suppressed; the recorded log drives the game instead, frame-for-frame.</summary>
		Replay
	}
}
