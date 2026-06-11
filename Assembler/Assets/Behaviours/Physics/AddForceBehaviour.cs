using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	/// <summary>Adds a continuous world-space force to the entity's Rigidbody when Executed (typically via a trigger).</summary>
	/// <remarks>
	/// Properties:
	///   Force: World-space force vector applied with ForceMode.Force (mass-dependent, frame-rate independent acceleration).
	/// </remarks>
	public sealed class AddForceBehaviour : GameBehaviour<AddForceData>, IAmExecutable
	{
		private Rigidbody _rigidbody;

		protected override void OnInitialise(AddForceData data)
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
				_rigidbody.AddForce(Data.Force.Get(ctx), ForceMode.Force);
			}
		}
	}
}
