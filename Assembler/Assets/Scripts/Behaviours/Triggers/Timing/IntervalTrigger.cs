using System.Collections;
using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using UnityEngine;

namespace AssemblerAlpha.Behaviours.Triggers.Timing
{
	public partial class IntervalTrigger : TimingTrigger<EveryInfo>
	{
		private int _intervalCount;
		private float _intervalDuration;

		protected override void OnInitialise(EveryInfo behaviourInfo)
		{
		}

		private IEnumerator Start()
		{
			for (int i = 0; _intervalCount == 0 || i < _intervalCount; i++)
			{
				yield return new WaitForSeconds(_intervalDuration);

				Execute();
			}
		}
	}
}