using System.Collections;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Timing
{
	public class TimerTrigger : TimingTrigger<TimerTriggerData>
	{
		private bool _started;

		private void Start()
		{
			if (_started) return;
			_started = true;
			Execute();
		}

		public override void OnPostInitialise()
		{
			if (_started) return;
			_started = true;
			Execute();
		}

		public override void OnDespawn()
		{
			StopAllCoroutines();
			_started = false;
		}

		public override void Execute()
		{
			StartCoroutine(InvokeTriggerAfter(Data.Delay.Value));
		}

		private IEnumerator InvokeTriggerAfter(float seconds)
		{
			yield return new WaitForSeconds(seconds);

			NotifyListeners();
		}
	}
}
