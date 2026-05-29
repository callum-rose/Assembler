using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Timing
{
	/// <summary>Forwards a trigger event only when no prior trigger has been received within the last Interval seconds. Use to suppress rapid repeat triggers.</summary>
	/// <remarks>
	/// Properties:
	///   Interval: Seconds that must elapse since the previous incoming trigger before another one is forwarded.
	/// </remarks>
	public sealed class DebouncedTrigger : Trigger<DebouncedTriggerData>
	{
		private float _lastTriggerTime = float.NegativeInfinity;

		public override void Execute(TriggerContext ctx)
		{
			var now = Time.time;
			var interval = Data.Interval.Get(ctx);

			if (now - _lastTriggerTime < interval)
			{
				_lastTriggerTime = now;
				return;
			}

			_lastTriggerTime = now;
			NotifyListeners(ctx);
		}
	}
}
