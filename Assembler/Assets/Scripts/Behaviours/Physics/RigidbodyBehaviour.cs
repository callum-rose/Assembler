using Core;
using UnityEngine;

namespace Behaviours.Physics
{
	[RequireComponent(typeof(Rigidbody))]
	public abstract class RigidbodyBehaviour : GameBehaviour
	{
		protected Rigidbody Rigidbody { get; private set; }

		private void Awake()
		{
			Rigidbody = GetComponent<Rigidbody>();
		}
	}

	public class Gravity : RigidbodyBehaviour
	{
		private bool _gravityEnabled;

		public override void Execute()
		{
			Rigidbody.useGravity = _gravityEnabled;
		}
	}

	public class Velocity : RigidbodyBehaviour
	{
		private Vector3 _velocity;

		public override void Execute()
		{
			Rigidbody.linearVelocity = _velocity;
		}
	}
}