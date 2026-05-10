using Assembler.Parsing.Phase2.Info;
using Assembler.Parsing.Phase3;
using UnityEngine;

namespace Assembler.Behaviours.Movement
{
	public class Velocity : PositionBehaviour<VelocityData>
	{
		private IValueProvider<Vector3> _velocity;

		public Velocity(IValueProvider<Vector3> velocity)
		{
			_velocity = velocity;
		}

		protected override void OnInitialise(VelocityData data)
		{
			_velocity = data.Velocity;
		}

		private void Update()
		{
			Execute();
		}

		public override void Execute()
		{
			transform.position += _velocity.Value * Time.deltaTime;
		}
	}
}