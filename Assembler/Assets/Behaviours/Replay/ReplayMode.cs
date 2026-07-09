namespace Assembler.Behaviours.Replay
{
	/// <summary>
	/// Whether an <see cref="InputReplaySession"/> is capturing input or replaying a captured log. There is no
	/// "off" state — the absence of a session (a null <see cref="InputReplayHub.Current"/>) is normal play, so a
	/// session always has exactly one of these two jobs.
	/// </summary>
	public enum ReplayMode
	{
		/// <summary>Live input drives the game, and every input-trigger emission is appended to the log.</summary>
		Record,

		/// <summary>Live device reads are suppressed; the recorded log drives the game instead, frame-for-frame.</summary>
		Replay
	}
}
