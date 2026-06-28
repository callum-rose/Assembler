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
	public sealed class SetVelocityBehaviour : RigidbodyGameBehaviour<SetVelocityData>
	{
		protected override void Apply(Rigidbody rigidbody, TriggerContext ctx) =>
			rigidbody.linearVelocity = Data.Velocity.Get(ctx);
	}
}
