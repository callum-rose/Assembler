using UnityEngine;

namespace Assembler.Behaviours.Triggers.Physical
{
	public class TriggerExit : PhysicalTrigger
	{
		private void OnTriggerExit(Collider other)
		{
			if (IsOtherRelevant(other.gameObject))
			{
				TriggerContext.Push();
				try
				{
					TriggerContext.Set("other_position", other.transform.position);
					InvokeListeners();
				}
				finally
				{
					TriggerContext.Pop();
				}
			}
		}
	}
}
