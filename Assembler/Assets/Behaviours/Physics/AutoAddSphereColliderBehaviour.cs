using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	/// <summary>Adds a Unity SphereCollider to the entity. Required for collision/trigger physics events.</summary>
	/// <remarks>
	/// Properties:
	///   Radius: Local-space radius of the sphere.
	///   IsTrigger: When true the collider fires trigger events (no physical collision response) instead of acting as a solid collider.
	///   Bounciness: Physics-material bounciness 0–1; when set (with any friction property) a PhysicsMaterial is created and assigned.
	///   DynamicFriction: Physics-material friction 0–1 applied while the surfaces are sliding.
	///   StaticFriction: Physics-material friction 0–1 applied while the surfaces are at rest.
	/// </remarks>
	public sealed class AutoAddSphereColliderBehaviour : AddColliderBehaviour<SphereColliderData>
	{
		protected override Collider CreateCollider(SphereColliderData data)
		{
			var collider = gameObject.AddComponent<SphereCollider>();
			data.Radius.UseIfValueExists(v => collider.radius = v);
			return collider;
		}
	}
}
