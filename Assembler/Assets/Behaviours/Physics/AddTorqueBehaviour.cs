using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	/// <summary>Adds a continuous world-space torque to the entity's Rigidbody when Executed (typically via a trigger).</summary>
	/// <remarks>
	/// Properties:
	///   Torque: World-space torque vector applied with ForceMode.Force (mass-dependent angular acceleration).
	/// </remarks>
	public sealed class AddTorqueBehaviour : GameBehaviour<AddTorqueData>
	{
		private Rigidbody _rigidbody;

		protected override void OnInitialise(AddTorqueData data)
		{
			_rigidbody = GetComponent<Rigidbody>();
		}

		public override void Execute()
		{
			if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
			if (_rigidbody != null) _rigidbody.AddTorque(Data.Torque.Value, ForceMode.Force);
		}
	}
}
