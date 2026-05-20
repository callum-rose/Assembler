using System.Collections;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Timing
{
	public sealed class DeferredTrigger : Trigger<DeferredTriggerData>
	{
		public override void Execute()
		{
			StartCoroutine(Routine());
			return;

			IEnumerator Routine()
			{
				yield return new WaitForSeconds(Data.Delay.Value);

				NotifyListeners();
			}
		}

		public override void OnDespawn()
		{
			StopAllCoroutines();
		}
	}
}