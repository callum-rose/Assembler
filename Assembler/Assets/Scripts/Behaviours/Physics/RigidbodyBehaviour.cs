using Core;
using UnityEngine;

namespace Behaviours.Physics
{
	[RequireComponent(typeof(Rigidbody))]
	public abstract class RigidbodyBehaviour : GameBehaviour
	{
		public new class Configuration : GameBehaviour.Configuration
		{
		}
		
		protected Rigidbody Rigidbody { get; private set; }

		private void Awake()
		{
			Rigidbody = GetComponent<Rigidbody>();
		}
	}

	public class Gravity : RigidbodyBehaviour
	{
		public new class Configuration : RigidbodyBehaviour.Configuration
		{
			public bool Enabled { get; set; }
		}
		
		private bool _gravityEnabled;

		public override void Execute()
		{
			Rigidbody.useGravity = _gravityEnabled;
		}

		protected override void OnInitialise(GameBehaviour.Configuration configuration)
		{
			base.OnInitialise(configuration);
			
			if (configuration is Configuration config)
			{
				_gravityEnabled = config.Enabled;
			}
			else
			{
				Debug.LogError("Invalid configuration type for Gravity behaviour");
			}
		}
	}

	public class Velocity : RigidbodyBehaviour
	{
		public new class Configuration : GameBehaviour.Configuration
		{
			public Vector3 Velocity { get; set; }
		}
		
		private Vector3 _velocity;

		public override void Execute()
		{
			Rigidbody.linearVelocity = _velocity;
		}

		protected override void OnInitialise(GameBehaviour.Configuration configuration)
		{
			base.OnInitialise(configuration);
			
			if (configuration is Configuration config)
			{
				_velocity = config.Velocity;
			}
			else
			{
				Debug.LogError("Invalid configuration type for Velocity behaviour");
			}
		}
	}
}