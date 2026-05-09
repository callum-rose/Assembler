using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using UnityEngine;

namespace AssemblerAlpha.Behaviours.Triggers.Physical
{
	public class CollisionEnter : PhysicalTrigger<CollisionEnterTriggerInfo>
	{
		protected override void OnInitialise(CollisionEnterTriggerInfo behaviourInfo)
		{
		}

		private void OnCollisionEnter(Collision other)
		{
			if (IsOtherRelevant(other.gameObject))
			{
				InvokeTrigger();
			}
		}
	}
}