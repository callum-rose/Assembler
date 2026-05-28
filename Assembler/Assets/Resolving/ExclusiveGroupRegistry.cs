using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving
{
	/// <summary>
	/// Tracks, per group name, the last Unity frame on which any trigger in that group fired.
	/// Used by <c>ExclusiveTrigger</c> to enforce "first-to-fire wins" semantics across a group.
	/// One instance is built per game build and shared across all triggers.
	/// </summary>
	public sealed class ExclusiveGroupRegistry
	{
		private readonly Dictionary<string, int> _lastClaimedFrame = new();

		/// <summary>
		/// Attempts to claim the group for the current frame.
		/// Returns true if the group has not yet been claimed this frame (and records the claim);
		/// returns false if another caller has already claimed it this frame.
		/// </summary>
		public bool TryClaim(string group)
		{
			int frame = Time.frameCount;
			if (_lastClaimedFrame.TryGetValue(group, out int last) && last == frame)
			{
				return false;
			}

			_lastClaimedFrame[group] = frame;
			return true;
		}
	}
}
