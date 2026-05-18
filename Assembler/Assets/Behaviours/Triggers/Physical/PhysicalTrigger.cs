using System.Linq;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Physical
{
	public abstract class PhysicalTrigger : Trigger<PhysicalTriggerData>
	{
		protected bool IsOtherRelevant(GameObject other)
		{
			var otherGameEntity = other.GetComponent<GameEntity>();
			return otherGameEntity != null && otherGameEntity.Tags.Intersect(Data.TagsToDetect).Any();
		}
	}
}
