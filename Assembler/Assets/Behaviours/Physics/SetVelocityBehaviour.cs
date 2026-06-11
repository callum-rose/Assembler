using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	/// <summary>Sets the entity's Rigidbody linear velocity to <c>Velocity</c> when Executed (typically via a trigger).</summary>
	/// <remarks>
	/// Properties:
	///   Velocity: World-space linear velocity in units per second.
	/// </remarks>
	public sealed class SetVelocityBehaviour : GameBehaviour<SetVelocityData>, IAmExecutable
	{
		private Rigidbody _rigidbody;

		protected override void OnInitialise(SetVelocityData data)
		{
			_rigidbody = GetComponent<Rigidbody>();
		}

		public void Execute(TriggerContext ctx)
		{
			if (_rigidbody == null)
			{
				_rigidbody = GetComponent<Rigidbody>();
			}

			if (_rigidbody != null)
			{
				_rigidbody.linearVelocity = Data.Velocity.Get(ctx);
			}
		}
	}
}
