using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	/// <summary>Adds a Unity Rigidbody to the entity so it participates in physics simulation.</summary>
	/// <remarks>
	/// Properties:
	///   UseGravity: When true the rigidbody is affected by gravity.
	/// </remarks>
	public sealed class RigidbodyBehaviour : GameBehaviour<RigidbodyData>
	{
		private Rigidbody _rigidbody;

		protected override void OnInitialise(RigidbodyData data)
		{
			_rigidbody = gameObject.AddComponent<Rigidbody>();
			data.IsKinematic.UseIfValueExists(v => _rigidbody.isKinematic = v);
			data.UseGravity.UseIfValueExists(v => _rigidbody.useGravity = v);
		}

		public override void Execute() { }
	}
}