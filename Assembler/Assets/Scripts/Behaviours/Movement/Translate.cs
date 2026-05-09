using UnityEngine;

namespace Behaviours.Movement
{
	public partial class Translate : PositionBehaviour<TranslateBehaviourInfo>
	{
		private Vector3 displacement;
		
		public override void Execute()
		{
			transform.position += displacement * Time.deltaTime;
		}
	}
}