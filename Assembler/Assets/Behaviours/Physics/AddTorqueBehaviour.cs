using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	/// <summary>Adds a continuous world-space torque to the entity's Rigidbody when Executed (typically via a trigger).</summary>
	/// <remarks>
	/// Applied as an <see cref="ForceMode.Impulse"/> scaled by the elapsed time of the step it is fired in, for the
	/// same reason as <c>add force</c>: a raw <see cref="ForceMode.Force"/> is only correct once per physics step, so
	/// fired from a per-frame trigger it would make angular acceleration scale with the refresh rate. Scaling by the
	/// step delta uses the game-clock delta from a per-frame trigger (frame-rate independent, zero while paused) and
	/// the fixed timestep from a fixed-update/collision trigger (identical to the old <c>ForceMode.Force</c> there).
	/// Properties:
	///   Torque: World-space torque vector (mass-dependent; applied continuously, frame-rate independent).
	/// </remarks>
	public sealed class AddTorqueBehaviour : RigidbodyGameBehaviour<AddTorqueData>
	{
		protected override void Apply(Rigidbody rigidbody, TriggerContext ctx) =>
			rigidbody.AddTorque(Data.Torque.Get(ctx) * StepDelta, ForceMode.Impulse);
	}
}
