using System.Linq;
using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using AssemblerAlpha.Core;
using UnityEngine;

namespace AssemblerAlpha.Behaviours.Triggers.Physical
{
	public abstract class PhysicalTrigger<T> : Trigger<T> where T : BehaviourInfo
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