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
	///   Bounciness: Physics-material bounciness 0–1; when set (with any friction property) a PhysicsMaterial is created and assigned.
	///   DynamicFriction: Physics-material friction 0–1 applied while the surfaces are sliding.
	///   StaticFriction: Physics-material friction 0–1 applied while the surfaces are at rest.
	/// </remarks>
	public sealed class AutoAddCapsuleColliderBehaviour : AddColliderBehaviour<CapsuleColliderData>
	{
		protected override Collider CreateCollider() => gameObject.AddComponent<CapsuleCollider>();

		protected override void ApplyShape(Collider collider, CapsuleColliderData data)
		{
			var capsule = (CapsuleCollider)collider;
			data.Radius.UseIfValueExists(v => capsule.radius = v);
			data.Height.UseIfValueExists(v => capsule.height = v);
			data.Direction.UseIfValueExists(v => capsule.direction = v);
		}
	}
}
