using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using AssemblerAlpha.Core;
using UnityEngine;

namespace AssemblerAlpha.Behaviours.Physics
{
	[RequireComponent(typeof(Rigidbody))]
	public abstract class RigidbodyBehaviour<T> : GameBehaviour<T> where T : BehaviourInfo
	{
		protected Rigidbody Rigidbody { get; private set; }

		private void Awake()
		{
			Rigidbody = GetComponent<Rigidbody>();
		}
	}

	public class Gravity : RigidbodyBehaviour<RigidbodyInfo>
	{
		private bool _gravityEnabled;

		protected override void OnInitialise(RigidbodyInfo behaviourInfo)
		{
			_gravityEnabled = behaviourInfo.UseGravity;
		}

		public override void Execute()
		{
			Rigidbody.useGravity = _gravityEnabled;
		}
	}

	public class Velocity : RigidbodyBehaviour<VelocityInfo>
	{
		private Vector3 _velocity;

		public override void Execute()
		{
			Rigidbody.linearVelocity = _velocity;
		}
	}
}