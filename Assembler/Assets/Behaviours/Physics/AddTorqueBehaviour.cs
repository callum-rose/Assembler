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
	public sealed class AddTorqueBehaviour : RigidbodyGameBehaviour<AddTorqueData>
	{
		protected override void Apply(Rigidbody rigidbody, TriggerContext ctx) =>
			rigidbody.AddTorque(Data.Torque.Get(ctx), ForceMode.Force);
	}
}
