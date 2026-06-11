using Assembler.Resolving;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Physical
{
	/// <summary>Fires every physics frame while an entity matching TagsToDetect stays inside this entity's trigger collider.</summary>
	/// <remarks>
	/// Properties:
	///   TagsToDetect: Only fire while the other entity has at least one of these tags. Leave empty to fire on any entity.
	/// Outputs:
	///   other_position [Vector3]: Other entity's world position this frame.
	/// </remarks>
	public class TriggerStay : PhysicalTrigger
	{
		private void OnTriggerStay(Collider other)
		{
			if (IsOtherRelevant(other.gameObject))
			{
				NotifyListeners(TriggerContext.New("other_position", other.transform.position));
			}
		}
	}
}
