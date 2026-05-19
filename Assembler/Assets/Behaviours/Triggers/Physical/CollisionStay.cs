using UnityEngine;

namespace Assembler.Behaviours.Triggers.Physical
{
	public class CollisionStay : PhysicalTrigger
	{
		private void OnCollisionStay(Collision other)
		{
			if (IsOtherRelevant(other.gameObject))
			{
				TriggerContext.Push();
				try
				{
					TriggerContext.Set("contact_point", other.contacts[0].point);
					TriggerContext.Set("contact_normal", other.contacts[0].normal);
					TriggerContext.Set("other_velocity",
						other.rigidbody != null ? other.rigidbody.linearVelocity : Vector3.zero);
					TriggerContext.Set("other_position", other.transform.position);
					NotifyListeners();
				}
				finally
				{
					TriggerContext.Pop();
				}
			}
		}
	}
}
