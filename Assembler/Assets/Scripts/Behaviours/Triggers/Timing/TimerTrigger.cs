using System.Collections;
using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using UnityEngine;

namespace AssemblerAlpha.Behaviours.Triggers.Timing
{
	public partial class TimerTrigger : TimingTrigger<AfterInfo>
	{
		private float _duration;

		protected override void OnInitialise(AfterInfo behaviourInfo)
		{
		}

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