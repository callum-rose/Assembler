using System.Linq;
using Core;
using UnityEngine;

namespace Behaviours.Triggers.Physical
{
	public abstract class PhysicalTrigger : Trigger
	{
		private string[] tags;

		protected bool IsOtherRelevant(GameObject gameObject)
		{
			return gameObject.GetComponentInParent<GameEntity>() is var entity &&
			       entity != null &&
			       entity.Tags.Intersect(tags).Any();
		}
	}
}