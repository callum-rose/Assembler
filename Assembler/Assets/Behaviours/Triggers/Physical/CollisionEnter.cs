using UnityEngine;

namespace Assembler.Behaviours.Triggers.Physical
{
	/// <summary>Fires when a non-trigger collision begins with another entity matching TagsToDetect. Requires colliders + a Rigidbody.</summary>
	/// <remarks>
	/// Properties:
	///   TagsToDetect: Only fire when the other entity has at least one of these tags.
	/// Outputs:
	///   contact_point [Vector3]: World-space point of first contact.
	///   contact_normal [Vector3]: Surface normal at the contact point.
	///   other_velocity [Vector3]: Other body's linear velocity (zero if it has no Rigidbody).
	///   other_position [Vector3]: Other entity's world position at the moment of collision.
	/// </remarks>
	public class CollisionEnter : PhysicalTrigger
	{
		private void OnCollisionEnter(Collision other)
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
