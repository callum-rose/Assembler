using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	/// <summary>Adds a Unity BoxCollider to the entity, sized to <c>Size</c>. Required for collision/trigger physics events.</summary>
	/// <remarks>
	/// Properties:
	///   Size: Local-space dimensions of the box (x, y, z).
	///   IsTrigger: When true the collider fires trigger events (no physical collision response) instead of acting as a solid collider.
	///   Bounciness: Physics-material bounciness 0–1; when set (with any friction property) a PhysicsMaterial is created and assigned.
	///   DynamicFriction: Physics-material friction 0–1 applied while the surfaces are sliding.
	///   StaticFriction: Physics-material friction 0–1 applied while the surfaces are at rest.
	/// </remarks>
	public sealed class AutoAddBoxColliderBehaviour : AddColliderBehaviour<BoxColliderData>
	{
		protected override Collider CreateCollider() => gameObject.AddComponent<BoxCollider>();

		protected override void ApplyShape(Collider collider, BoxColliderData data) =>
			data.Size.UseIfValueExists(v => ((BoxCollider)collider).size = v);
	}
}
