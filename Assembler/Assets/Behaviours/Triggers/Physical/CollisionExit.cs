using UnityEngine;

namespace Assembler.Behaviours.Triggers.Physical
{
	public class CollisionExit : PhysicalTrigger
	{
		private void OnCollisionExit(Collision other)
		{
			if (IsOtherRelevant(other.gameObject))
			{
				TriggerContext.Push();
				try
				{
					TriggerContext.Set("other_velocity",
						other.rigidbody != null ? other.rigidbody.linearVelocity : Vector3.zero);
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
