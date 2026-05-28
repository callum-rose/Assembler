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
	public sealed class AddImpulseBehaviour : GameBehaviour<AddImpulseData>
	{
		private Rigidbody _rigidbody;

		protected override void OnInitialise(AddImpulseData data)
		{
			_rigidbody = GetComponent<Rigidbody>();
		}

		public override void Execute()
		{
			if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
			if (_rigidbody != null) _rigidbody.AddForce(Data.Impulse.Value, ForceMode.Impulse);
		}
	}
}
