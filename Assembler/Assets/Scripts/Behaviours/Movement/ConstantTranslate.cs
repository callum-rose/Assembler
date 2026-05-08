using UnityEngine;

namespace Behaviours.Movement
{
	public partial class ConstantTranslate : MovementBehaviour
	{
		private Vector3 displacement;
		
		public override void Execute()
		{
			transform.position += displacement * Time.deltaTime;
		}
	}
}