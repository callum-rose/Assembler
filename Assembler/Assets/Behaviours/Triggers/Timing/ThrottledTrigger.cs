using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Timing
{
	/// <summary>Forwards at most Rate trigger events per second. Incoming triggers that arrive sooner than 1/Rate seconds after the previous forwarded one are dropped.</summary>
	/// <remarks>
	/// Properties:
	///   Rate: Maximum number of forwarded triggers per second.
	/// </remarks>
	public sealed class ThrottledTrigger : Trigger<ThrottledTriggerData>
	{
		private float _lastTriggerTime = float.NegativeInfinity;

		public override void Execute()
		{
			var rate = Data.Rate.Value;
			if (rate <= 0f)
			{
				return;
			}

			var now = Time.time;
			var minInterval = 1f / rate;

			if (now - _lastTriggerTime < minInterval)
			{
				return;
			}

			_lastTriggerTime = now;
			NotifyListeners();
		}
	}
}
