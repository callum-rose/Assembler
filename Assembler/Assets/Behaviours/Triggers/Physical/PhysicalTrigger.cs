using System;
using System.Linq;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Physical
{
	/// <summary>
	/// Base class for triggers that fire from physics collision/trigger events. These cannot be executed manually
	/// (<see cref="Execute"/> throws); subclasses notify listeners from Unity collision callbacks instead. The
	/// <see cref="IsOtherRelevant"/> helper filters incoming contacts to entities matching TagsToDetect.
	/// </summary>
	/// <remarks>
	/// Properties:
	///   TagsToDetect: Entity tags a colliding object must have for the trigger to fire. Leave empty to fire on
	///                 collisions with any entity.
	/// </remarks>
	public abstract class PhysicalTrigger : Trigger<PhysicalTriggerData>
	{
		public override void Execute(TriggerContext ctx)
		{
			throw new Exception("Cannot execute an input trigger manually");
		}

		protected bool IsOtherRelevant(GameObject other)
		{
			var otherGameEntity = other.GetComponent<GameEntity>();
			if (otherGameEntity == null)
			{
				return false;
			}

			// An empty TagsToDetect means "no tag filter" — fire on a collision with any entity.
			return Data.TagsToDetect.Count == 0 || otherGameEntity.Tags.Intersect(Data.TagsToDetect).Any();
		}
	}
}
