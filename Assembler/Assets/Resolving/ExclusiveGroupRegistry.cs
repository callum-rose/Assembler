using System.Collections.Generic;
using Assembler.Time;

namespace Assembler.Resolving
{
	/// <summary>
	/// Tracks, per group name, the last game frame on which any trigger in that group fired.
	/// Used by <c>ExclusiveTrigger</c> to enforce "first-to-fire wins" semantics across a group.
	/// One instance is built per game build and shared across all triggers.
	/// </summary>
	/// <remarks>
	/// Uses the injected game clock's frame count, which freezes while paused — so "first-to-fire
	/// this frame" exclusivity spans paused real-frames. That is acceptable: game logic is frozen
	/// while paused anyway.
	/// </remarks>
	public sealed class ExclusiveGroupRegistry
	{
		private readonly Dictionary<string, int> _lastClaimedFrame = new();
		private readonly IGameClock _clock;

		public ExclusiveGroupRegistry(IGameClock clock)
		{
			_clock = clock;
		}

		/// <summary>
		/// Attempts to claim the group for the current frame.
		/// Returns true if the group has not yet been claimed this frame (and records the claim);
		/// returns false if another caller has already claimed it this frame.
		/// </summary>
		public bool TryClaim(string group)
		{
			int frame = _clock.FrameCount;
			if (_lastClaimedFrame.TryGetValue(group, out int last) && last == frame)
			{
				return false;
			}

			_lastClaimedFrame[group] = frame;
			return true;
		}
	}
}
