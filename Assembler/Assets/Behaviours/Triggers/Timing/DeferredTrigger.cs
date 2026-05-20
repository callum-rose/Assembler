using System.Collections;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Timing
{
	/// <summary>Forwards a trigger event to listeners after a delay. Insert between an upstream trigger and downstream behaviours to defer execution.</summary>
	/// <remarks>
	/// Properties:
	///   Delay: Seconds to wait between Execute and notifying listeners.
	/// </remarks>
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
	}
}