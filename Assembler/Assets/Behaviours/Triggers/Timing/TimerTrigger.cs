using System.Collections;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Timing
{
	public class TimerTrigger : TimingTrigger<TimerTriggerData>
	{
		private void Start()
		{
			Execute();
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