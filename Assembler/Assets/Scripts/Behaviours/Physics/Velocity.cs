using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using UnityEngine;

namespace AssemblerAlpha.Behaviours.Physics
{
	public class Velocity : RigidbodyBehaviour<VelocityInfo>
	{
		private Vector3 _velocity;

		public override void Execute()
		{
			Rigidbody.linearVelocity = _velocity;
		}
	}
}