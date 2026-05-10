using UnityEngine;

namespace Assembler.Behaviours.Triggers.Physical
{
	public class CollisionStay : PhysicalTrigger
	{
		private void OnCollisionStay(Collision other)
		{
			if (IsOtherRelevant(other.gameObject))
			{
				Execute();
			}
		}
	}
}