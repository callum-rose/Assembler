using UnityEngine;

namespace Behaviours.Movement
{
	public partial class ConstantVelocity : MovementBehaviour
	{
		private Vector3 velocity;

		private void Update()
		{
			Execute();
		}

		public override void Execute()
		{
			transform.position += velocity * Time.deltaTime;
		}
	}
}