using System.Collections;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Timing
{
	/// <summary>Fires once after a delay.</summary>
	/// <remarks>
	/// Properties:
	///   Delay: Seconds to wait before notifying listeners.
	///   AutoStart: When true the countdown starts on entity start; when false it waits for an Execute call from upstream.
	/// </remarks>
	public class TimerTrigger : TimingTrigger<TimerTriggerData>, INeedsGameClock
	{
		public IGameClock Clock { get; set; } = null!;

		private Coroutine? _currentCoroutine;

		private void Start()
		{
			if (Data.AutoStart.Get(TriggerContext.Empty))
				Execute(TriggerContext.Empty);
		}

		public override void Execute(TriggerContext ctx)
		{
			if (_currentCoroutine is not null)
				StopCoroutine(_currentCoroutine);

			var captured = ctx;
			_currentCoroutine = StartCoroutine(InvokeTriggerAfter(Data.Delay.Get(ctx), captured));
		}

		private IEnumerator InvokeTriggerAfter(float seconds, TriggerContext captured)
		{
			yield return new WaitForGameSeconds(Clock, seconds);

			_currentCoroutine = null;
			NotifyListeners(captured);
		}
	}
}
