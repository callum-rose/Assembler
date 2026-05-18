using UnityEngine;

namespace Assembler.Behaviours.Triggers.Physical
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