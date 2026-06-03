using Assembler.Resolving;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Physical
{
	/// <summary>Fires when a non-trigger collision begins with another entity matching TagsToDetect. Requires colliders + a Rigidbody.</summary>
	/// <remarks>
	/// Properties:
	///   TagsToDetect: Only fire when the other entity has at least one of these tags. Leave empty to fire on any entity.
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
				NotifyListeners(TriggerContext.New(b =>
				{
					b["contact_point"] = other.contacts[0].point;
					b["contact_normal"] = other.contacts[0].normal;
					b["other_velocity"] = other.rigidbody != null ? other.rigidbody.linearVelocity : Vector3.zero;
					b["other_position"] = other.transform.position;
				}));
			}
		}
	}
}
