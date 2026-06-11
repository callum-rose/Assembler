using System.Collections;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;

namespace Assembler.Behaviours.Triggers.Timing
{
	/// <summary>Forwards a trigger event to listeners after a delay. Insert between an upstream trigger and downstream behaviours to defer execution.</summary>
	/// <remarks>
	/// Properties:
	///   Delay: Seconds to wait between Execute and notifying listeners.
	/// </remarks>
	public sealed class DeferredTrigger : Trigger<DeferredTriggerData>, INeedsGameClock, IAmExecutable
	{
		public IGameClock Clock { get; set; } = null!;

		public void Execute(TriggerContext ctx)
		{
			var captured = ctx;
			var delay = Data.Delay.Get(ctx);
			StartCoroutine(Routine());
			return;

			IEnumerator Routine()
			{
				yield return new WaitForGameSeconds(Clock, delay);

				NotifyListeners(captured);
			}
		}
	}
}
