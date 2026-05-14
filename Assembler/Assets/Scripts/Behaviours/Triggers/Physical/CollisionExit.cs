using UnityEngine;

namespace Assembler.Behaviours.Triggers.Physical
{
	public class CollisionExit : PhysicalTrigger
	{
		private void OnCollisionExit(Collision other)
		{
			if (IsOtherRelevant(other.gameObject))
			{
				Execute();
			}
		}
	}
}