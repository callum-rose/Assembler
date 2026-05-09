using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using UnityEngine;

namespace AssemblerAlpha.Behaviours.Triggers.Physical
{
	public class CollisionStay : PhysicalTrigger<CollisionStayTriggerInfo>
	{
		protected override void OnInitialise(CollisionStayTriggerInfo behaviourInfo)
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