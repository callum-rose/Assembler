using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Movement
{
	public class Velocity : GameBehaviour<VelocityData>
	{
		private void Update()
		{
			Execute();
		}

		public override void Execute()
		{
			transform.position += Data.Velocity.Value * Time.deltaTime;
		}
	}
}