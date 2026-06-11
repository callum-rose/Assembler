using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	/// <summary>Sets the entity's Rigidbody angular velocity to <c>AngularVelocity</c> when Executed (typically via a trigger).</summary>
	/// <remarks>
	/// Properties:
	///   AngularVelocity: World-space angular velocity in radians per second around each axis.
	/// </remarks>
	public sealed class SetAngularVelocityBehaviour : GameBehaviour<SetAngularVelocityData>, IAmExecutable
	{
		private Rigidbody _rigidbody;

		protected override void OnInitialise(SetAngularVelocityData data)
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
				_rigidbody.angularVelocity = Data.AngularVelocity.Get(ctx);
			}
		}
	}
}
