using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using UnityEngine;

namespace AssemblerAlpha.Behaviours.Movement
{
	public partial class Velocity : PositionBehaviour<VelocityInfo>
	{
		private Vector3 velocity;

		protected override void OnInitialise(VelocityInfo behaviourInfo)
		{
			
		}

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