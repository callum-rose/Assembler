using UnityEngine;

namespace Behaviours.Triggers.Physical
{
	public class TriggerExit : PhysicalTrigger
	{
		private void OnTriggerExit(Collider other)
		{
			if (IsOtherRelevant(other.gameObject))
			{
				Execute();
			}
		}
	}
}