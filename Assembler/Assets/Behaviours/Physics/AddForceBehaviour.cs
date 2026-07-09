using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	/// <summary>Adds a continuous world-space force to the entity's Rigidbody when Executed (typically via a trigger).</summary>
	/// <remarks>
	/// Applied as an <see cref="ForceMode.Impulse"/> scaled by the elapsed time of the step it is fired in, instead
	/// of a raw <see cref="ForceMode.Force"/>. A raw <c>ForceMode.Force</c> is only correct when applied exactly once
	/// per physics step; fired from a per-frame trigger (key hold / every frame) it runs once per <em>render</em>
	/// frame and so applies a full force per frame, making thrust scale with the refresh rate. Scaling by the step
	/// delta fixes that: driven from a per-frame trigger it uses the game-clock delta (frame-rate independent, and
	/// zero while paused); driven from a fixed-update or collision trigger it uses the fixed timestep, which is
	/// mathematically identical to the old <c>ForceMode.Force</c> per physics step (so existing fixed-step usage is
	/// unchanged). For an instantaneous kick, use <c>add impulse</c>.
	/// Properties:
	///   Force: World-space force vector (mass-dependent; applied continuously, frame-rate independent).
	/// </remarks>
	public sealed class AddForceBehaviour : RigidbodyGameBehaviour<AddForceData>
	{
		protected override void Apply(Rigidbody rigidbody, TriggerContext ctx) =>
			rigidbody.AddForce(Data.Force.Get(ctx) * StepDelta, ForceMode.Impulse);
	}
}
