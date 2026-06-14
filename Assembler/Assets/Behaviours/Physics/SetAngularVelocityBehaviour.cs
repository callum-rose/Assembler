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
	public sealed class SetAngularVelocityBehaviour : RigidbodyGameBehaviour<SetAngularVelocityData>
	{
		protected override void Apply(Rigidbody rigidbody, TriggerContext ctx) =>
			rigidbody.angularVelocity = Data.AngularVelocity.Get(ctx);
	}
}
