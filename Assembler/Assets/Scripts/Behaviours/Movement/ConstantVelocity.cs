using Assembler.Generators.Attributes;
using Core;
using UnityEngine;

namespace Behaviours.Movement
{
	public partial class ConstantVelocity : MovementBehaviour
	{
		[Inject("Velocity")]  private Vector3 velocity;

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