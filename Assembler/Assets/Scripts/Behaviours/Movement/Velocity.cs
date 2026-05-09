using UnityEngine;

namespace Behaviours.Movement
{
	public partial class Velocity : PositionBehaviour<VelocityBehaviourInfo>
	{
		private Vector3 velocity;

		protected override void OnInitialise(VelocityBehaviourInfo behaviourInfo)
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