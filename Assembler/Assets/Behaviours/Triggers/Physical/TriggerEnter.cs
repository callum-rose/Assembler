using UnityEngine;

namespace Assembler.Behaviours.Triggers.Physical
{
	public class TriggerEnter : PhysicalTrigger
	{
		private void OnTriggerEnter(Collider other)
		{
			if (IsOtherRelevant(other.gameObject))
			{
				InvokeListeners();
			}
		}
	}
}