using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	/// <summary>Adds an instantaneous world-space impulse to the entity's Rigidbody when Executed (typically via a trigger).</summary>
	/// <remarks>
	/// Properties:
	///   Impulse: World-space impulse applied with ForceMode.Impulse (mass-dependent, instantaneous velocity change).
	/// </remarks>
	public sealed class AddImpulseBehaviour : RigidbodyGameBehaviour<AddImpulseData>
	{
		protected override void Apply(Rigidbody rigidbody, TriggerContext ctx) =>
			rigidbody.AddForce(Data.Impulse.Get(ctx), ForceMode.Impulse);
	}
}
