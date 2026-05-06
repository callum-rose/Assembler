using System.Linq;
using Assembler.Generators.Attributes;
using Core;
using UnityEngine;

namespace Behaviours.Triggers.Physical
{
	public abstract class PhysicalTrigger : Trigger
	{
		[Inject("Tags")] private string[] tags;

		protected bool IsOtherRelevant(GameObject gameObject)
		{
			return gameObject.GetComponentInParent<GameEntity>() is var entity &&
			       entity != null &&
			       entity.Tags.Intersect(tags).Any();
		}
	}
}