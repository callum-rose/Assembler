using Assembler.Resolving;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Physical
{
	/// <summary>Fires when a non-trigger collision ends with another entity matching TagsToDetect.</summary>
	/// <remarks>
	/// Properties:
	///   TagsToDetect: Only fire when the other entity has at least one of these tags.
	/// Outputs:
	///   other_velocity [Vector3]: Other body's linear velocity at separation (zero if no Rigidbody).
	///   other_position [Vector3]: Other entity's world position at separation.
	/// </remarks>
	public class CollisionExit : PhysicalTrigger
	{
		private void OnCollisionExit(Collision other)
		{
			if (IsOtherRelevant(other.gameObject))
			{
				NotifyListeners(TriggerContext.New(b =>
				{
					b["other_velocity"] = other.rigidbody != null ? other.rigidbody.linearVelocity : Vector3.zero;
					b["other_position"] = other.transform.position;
				}));
			}
		}
	}
}
