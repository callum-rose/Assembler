using System.Linq;
using Assembler.Core;
using Assembler.Parsing.Phase3;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Physical
{
	public abstract class PhysicalTrigger : Trigger<PhysicalTriggerData>
	{
		protected bool IsOtherRelevant(GameObject gameObject)
		{
			return gameObject.GetComponentInParent<GameEntity>() is var entity &&
			       entity != null &&
			       entity.Tags.Intersect(Data.TagsToDetect).Any();
		}
	}
}
