using UnityEngine;

namespace Behaviours.Triggers.Physical
{
	public class CollisionEnter : PhysicalTrigger
	{
		private void OnCollisionEnter(Collision other)
		{
			if (IsOtherRelevant(other.gameObject))
			{
				InvokeTrigger();
			}
		}
	}
}