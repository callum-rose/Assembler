using Assembler.Behaviours.Triggers;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;

namespace Assembler.Behaviours.Gating
{
	/// <summary>Forwards a trigger event only when no prior trigger has been received within the last Interval seconds. Use to suppress rapid repeat triggers.</summary>
	/// <remarks>
	/// Properties:
	///   Interval: Seconds that must elapse since the previous incoming trigger before another one is forwarded.
	/// </remarks>
	public sealed class DebouncedTrigger : Trigger<DebouncedTriggerData>, INeedsGameClock, IAmExecutable
	{
		public IGameClock Clock { get; set; } = null!;

		private double _lastTriggerTime = double.NegativeInfinity;

		// Forget the previous life's last-trigger time so a pooled reuse forwards its first trigger immediately
		// rather than debouncing against a stale timestamp.
		public override void OnReuse() => _lastTriggerTime = double.NegativeInfinity;

		public void Execute(TriggerContext ctx)
		{
			var now = Clock.Time;
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
