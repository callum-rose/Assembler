using System.Collections;
using Assembler.Generators.Attributes;
using Core;
using UnityEngine;

namespace Behaviours.Triggers.Timing
{
	public partial class IntervalTrigger : TimingTrigger
	{
		[Inject("Interval Count", "The number of intervals to run for. If 0 run indefinitely.")]
		private int _intervalCount;

		[Inject("Interval Seconds", "The duration in seconds between each interval.")]
		private float _intervalDuration;

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