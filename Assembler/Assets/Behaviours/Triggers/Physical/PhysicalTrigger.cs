using System;
using System.Linq;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Physical
{
	public abstract class PhysicalTrigger : Trigger<PhysicalTriggerData>
	{
		public override void Execute()
		{
			throw new Exception("Cannot execute an input trigger manually");
		}
		
		protected bool IsOtherRelevant(GameObject other)
		{
			var otherGameEntity = other.GetComponent<GameEntity>();
			return otherGameEntity != null && otherGameEntity.Tags.Intersect(Data.TagsToDetect).Any();
		}
	}
}
