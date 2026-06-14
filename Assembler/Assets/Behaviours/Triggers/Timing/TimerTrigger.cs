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
	public class TimerTrigger : TimingTrigger<TimerTriggerData>, INeedsGameClock, IAmExecutable
	{
		public IGameClock Clock { get; set; } = null!;

		private Coroutine? _currentCoroutine;

		private void Start()
		{
			if (Data.AutoStart.Get(TriggerContext.Empty))
				Execute(TriggerContext.Empty);
		}

		// The previous life's coroutine was stopped when the shell deactivated; drop the stale handle, then
		// re-arm AutoStart — Unity's once-per-lifetime Start does not re-run on a reused component.
		public override void OnReuse()
		{
			_currentCoroutine = null;
			if (Data.AutoStart.Get(TriggerContext.Empty))
				Execute(TriggerContext.Empty);
		}

		public void Execute(TriggerContext ctx)
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
