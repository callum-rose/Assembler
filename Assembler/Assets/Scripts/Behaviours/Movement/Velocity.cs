using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using Assembler.Parsing.Phase3.Parsing.Phase3;
using AssemblerAlpha.Variables;
using UnityEngine;

namespace AssemblerAlpha.Behaviours.Movement
{
	public partial class Velocity : PositionBehaviour<VelocityInfo>
	{
		private ValueContainer<Vector3> velocity;

		// protected override void OnInitialise(VelocityData behaviourInfo)
		// {
		// 	velocity = behaviourInfo.Velocity;
		// }

		private void Update()
		{
			Execute();
		}

		public override void Execute()
		{
			transform.position += velocity.Value * Time.deltaTime;
		}
	}
}