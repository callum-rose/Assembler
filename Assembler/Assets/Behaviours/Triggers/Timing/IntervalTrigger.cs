using System.Collections;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Timing
{
	public class IntervalTrigger : TimingTrigger<IntervalTriggerData>
	{
		private void Start()
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