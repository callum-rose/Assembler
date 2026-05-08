using System.Collections;
using Core;
using UnityEngine;

namespace Behaviours.Triggers.Timing
{
	public partial class TimerTrigger : TimingTrigger
	{
		private float _duration;

		public override void Execute()
		{
			StartCoroutine(InvokeTriggerAfter(_duration));
		}

		private IEnumerator InvokeTriggerAfter(float seconds)
		{
			yield return new WaitForSeconds(seconds);

			InvokeTrigger();
		}
	}
}