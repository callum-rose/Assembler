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
	public sealed class AddForceBehaviour : RigidbodyGameBehaviour<AddForceData>
	{
		protected override void Apply(Rigidbody rigidbody, TriggerContext ctx) =>
			rigidbody.AddForce(Data.Force.Get(ctx), ForceMode.Force);
	}
}
