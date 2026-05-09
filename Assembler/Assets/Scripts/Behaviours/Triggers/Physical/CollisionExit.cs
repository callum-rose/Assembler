using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using UnityEngine;

namespace AssemblerAlpha.Behaviours.Triggers.Physical
{
	public class CollisionExit : PhysicalTrigger<CollisionExitTriggerInfo>
	{
		protected override void OnInitialise(CollisionExitTriggerInfo behaviourInfo)
		{
		}

		private void OnCollisionStay(Collision other)
		{
			if (IsOtherRelevant(other.gameObject))
			{
				Execute();
			}
		}
	}
}