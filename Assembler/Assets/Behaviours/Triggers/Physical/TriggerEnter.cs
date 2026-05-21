using UnityEngine;

namespace Assembler.Behaviours.Triggers.Physical
{
	/// <summary>Fires when an entity matching TagsToDetect enters this entity's trigger collider.</summary>
	/// <remarks>
	/// Properties:
	///   TagsToDetect: Only fire when the other entity has at least one of these tags.
	/// Outputs:
	///   other_position [Vector3]: Other entity's world position at the moment of entry.
	/// </remarks>
	public class TriggerEnter : PhysicalTrigger
	{
		private void OnTriggerEnter(Collider other)
		{
			if (IsOtherRelevant(other.gameObject))
			{
				TriggerContext.Push();
				try
				{
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
