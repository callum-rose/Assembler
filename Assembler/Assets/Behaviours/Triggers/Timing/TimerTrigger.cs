using System.Collections;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;

namespace Assembler.Behaviours.Triggers.Timing
{
	/// <summary>Fires once after a delay (starts the countdown on entity start, or on Execute).</summary>
	/// <remarks>
	/// Properties:
	///   Delay: Seconds to wait before notifying listeners.
	/// </remarks>
	public class TimerTrigger : TimingTrigger<TimerTriggerData>, INeedsGameClock
	{
		public IGameClock Clock { get; set; } = null!;

		private void Start()
		{
			Execute(TriggerContext.Empty);
		}

		public override void Execute(TriggerContext ctx)
		{
			var captured = ctx;
			StartCoroutine(InvokeTriggerAfter(Data.Delay.Get(ctx), captured));
		}

		private IEnumerator InvokeTriggerAfter(float seconds, TriggerContext captured)
		{
			yield return new WaitForGameSeconds(Clock, seconds);

			NotifyListeners(captured);
		}
	}
}