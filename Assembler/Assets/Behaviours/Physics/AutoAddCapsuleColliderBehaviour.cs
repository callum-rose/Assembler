using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	/// <summary>Adds a Unity CapsuleCollider to the entity. Required for collision/trigger physics events.</summary>
	/// <remarks>
	/// Properties:
	///   Radius: Local-space radius of the capsule hemispheres.
	///   Height: Local-space total height of the capsule along its Direction axis.
	///   Direction: Axis the capsule is aligned to — 0 = X, 1 = Y, 2 = Z.
	///   IsTrigger: When true the collider fires trigger events instead of acting as a solid collider.
	/// </remarks>
	public sealed class AutoAddCapsuleColliderBehaviour : GameBehaviour<CapsuleColliderData>
	{
		private CapsuleCollider _capsuleCollider;

		protected override void OnInitialise(CapsuleColliderData data)
		{
			_capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
			data.Radius.UseIfValueExists(TriggerContext.Empty, v => _capsuleCollider.radius = v);
			data.Height.UseIfValueExists(TriggerContext.Empty, v => _capsuleCollider.height = v);
			data.Direction.UseIfValueExists(TriggerContext.Empty, v => _capsuleCollider.direction = v);
			data.IsTrigger.UseIfValueExists(TriggerContext.Empty, v => _capsuleCollider.isTrigger = v);
		}

		public override void Execute(TriggerContext ctx) { }
	}
}
