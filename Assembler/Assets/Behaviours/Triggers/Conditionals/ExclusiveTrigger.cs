using System.Collections.Generic;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Conditionals
{
	/// <summary>Forwards an upstream trigger to listeners only if no other trigger sharing the same Group has already fired this frame.</summary>
	/// <remarks>
	/// Properties:
	///   Group: Name identifying the exclusion group; only the first trigger in this group to fire each frame propagates.
	/// </remarks>
	public class ExclusiveTrigger : Trigger<ExclusiveTriggerData>
	{
		public override void Execute()
		{
			if (ExclusiveGroupRegistry.TryClaim(Data.Group.Value))
			{
				NotifyListeners();
			}
		}
	}

	internal static class ExclusiveGroupRegistry
	{
		private static readonly Dictionary<string, int> LastClaimedFrame = new();

		public static bool TryClaim(string group)
		{
			int frame = Time.frameCount;
			if (LastClaimedFrame.TryGetValue(group, out int last) && last == frame)
			{
				return false;
			}

			LastClaimedFrame[group] = frame;
			return true;
		}
	}
}
