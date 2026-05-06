using UnityEngine;

namespace Behaviours.Triggers.Physical
{
	public class TriggerEnter : PhysicalTrigger
	{
		private void OnTriggerEnter(Collider other)
		{
			if (IsOtherRelevant(other.gameObject))
			{
				InvokeTrigger();
			}
		}
	}
}