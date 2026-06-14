using Assembler.Behaviours.Triggers;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;

namespace Assembler.Behaviours.Gating
{
	/// <summary>Forwards at most Rate trigger events per second. Incoming triggers that arrive sooner than 1/Rate seconds after the previous forwarded one are dropped.</summary>
	/// <remarks>
	/// Properties:
	///   Rate: Maximum number of forwarded triggers per second.
	/// </remarks>
	public sealed class ThrottledTrigger : Trigger<ThrottledTriggerData>, INeedsGameClock, IAmExecutable
	{
		public IGameClock Clock { get; set; } = null!;

		private double _lastTriggerTime = double.NegativeInfinity;

		// Forget the previous life's last-trigger time so a pooled reuse forwards its first trigger immediately
		// rather than throttling against a stale timestamp.
		public override void OnReuse() => _lastTriggerTime = double.NegativeInfinity;

		public void Execute(TriggerContext ctx)
		{
			var rate = Data.Rate.Get(ctx);
			if (rate <= 0f)
			{
				return;
			}

			var now = Clock.Time;
			var minInterval = 1f / rate;

			if (now - _lastTriggerTime < minInterval)
			{
				return;
			}

			_lastTriggerTime = now;
			NotifyListeners(ctx);
		}
	}
}
