using UnityEngine;

namespace Assembler.Behaviours.Triggers.Physical
{
	/// <summary>Fires every physics frame while colliding with another entity matching TagsToDetect.</summary>
	/// <remarks>
	/// Properties:
	///   TagsToDetect: Only fire when the other entity has at least one of these tags.
	/// Outputs:
	///   contact_point [Vector3]: World-space point of contact for this frame.
	///   contact_normal [Vector3]: Surface normal at the contact point.
	///   other_velocity [Vector3]: Other body's linear velocity (zero if no Rigidbody).
	///   other_position [Vector3]: Other entity's world position.
	/// </remarks>
	public class CollisionStay : PhysicalTrigger
	{
		private void OnCollisionStay(Collision other)
		{
			if (IsOtherRelevant(other.gameObject))
			{
				using (TriggerContext.Push())
				{
					TriggerContext.Set("contact_point", other.contacts[0].point);
					TriggerContext.Set("contact_normal", other.contacts[0].normal);
					TriggerContext.Set("other_velocity",
						other.rigidbody != null ? other.rigidbody.linearVelocity : Vector3.zero);
					TriggerContext.Set("other_position", other.transform.position);
					NotifyListeners();
				}
			}
		}
	}
}
