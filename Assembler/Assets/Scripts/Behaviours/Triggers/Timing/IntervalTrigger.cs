using System.Collections;
using Core;
using UnityEngine;

namespace Behaviours.Triggers.Timing
{
	public partial class IntervalTrigger : TimingTrigger
	{
		private int _intervalCount;
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