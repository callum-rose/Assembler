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
	/// </remarks>
	public sealed class AutoAddSphereColliderBehaviour : GameBehaviour<SphereColliderData>
	{
		private SphereCollider _sphereCollider;
		
		protected override void OnInitialise(SphereColliderData data)
		{
			_sphereCollider = gameObject.AddComponent<SphereCollider>();
			data.Radius.UseIfValueExists(v => _sphereCollider.radius = v);
			data.IsTrigger.UseIfValueExists(v => _sphereCollider.isTrigger = v);
		}

		public override void Execute(TriggerContext ctx)
		{
		}
	}
}