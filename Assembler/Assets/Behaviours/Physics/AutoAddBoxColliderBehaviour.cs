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
	public sealed class AutoAddBoxColliderBehaviour : GameBehaviour<BoxColliderData>
	{
		private BoxCollider _boxCollider;
		private PhysicsMaterial? _physicsMaterial;

		protected override void OnInitialise(BoxColliderData data)
		{
			_boxCollider = gameObject.AddComponent<BoxCollider>();
			data.Size.UseIfValueExists(v => _boxCollider.size = v);
			data.IsTrigger.UseIfValueExists(v => _boxCollider.isTrigger = v);
			_physicsMaterial = data.Material.ApplyTo(_boxCollider);
		}

		private void OnDestroy() => PhysicsMaterialProviders.Cleanup(_physicsMaterial);

		public override void Execute(TriggerContext ctx)
		{
		}
	}
}
