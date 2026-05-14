using System.Collections;
using Assembler.Resolving;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Timing
{
	public class IntervalTrigger : TimingTrigger<IntervalTriggerData>
	{
		public override void Execute()
		{
			StartCoroutine(Routine(Data.Interval.Value, Data.Count.Value));
		}

		private IEnumerator Routine(float interval, int count)
		{
			for (int i = 0; count == 0 || i < count; i++)
			{
				yield return new WaitForSeconds(interval);

				InvokeListeners();
			}
		}
	}
}