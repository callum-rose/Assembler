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
	/// </remarks>
	public sealed class AutoAddBoxColliderBehaviour : GameBehaviour<BoxColliderData>
	{
		private BoxCollider _boxCollider;
		
		protected override void OnInitialise(BoxColliderData data)
		{
			if (!gameObject.TryGetComponent(out _boxCollider))
			{
				_boxCollider = gameObject.AddComponent<BoxCollider>();
			}
			data.Size.UseIfValueExists(v => _boxCollider.size = v);
			data.IsTrigger.UseIfValueExists(v => _boxCollider.isTrigger = v);
		}

		public override void Execute()
		{
		}
	}
}