using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	/// <summary>Adds a Unity MeshCollider to the entity using the mesh from the entity's MeshFilter. Required for collision/trigger physics events on arbitrary meshes (e.g. voxel meshes).</summary>
	/// <remarks>
	/// Properties:
	///   Convex: When true the collider uses a convex hull (required for non-kinematic Rigidbodies and trigger volumes).
	///   IsTrigger: When true the collider fires trigger events instead of acting as a solid collider (requires Convex = true).
	///   Bounciness: Physics-material bounciness 0–1; when set (with any friction property) a PhysicsMaterial is created and assigned.
	///   DynamicFriction: Physics-material friction 0–1 applied while the surfaces are sliding.
	///   StaticFriction: Physics-material friction 0–1 applied while the surfaces are at rest.
	/// </remarks>
	public sealed class AutoAddMeshColliderBehaviour : GameBehaviour<MeshColliderData>
	{
		private MeshCollider _meshCollider;
		private PhysicsMaterial? _physicsMaterial;

		protected override void OnInitialise(MeshColliderData data)
		{
			_meshCollider = gameObject.AddComponent<MeshCollider>();
			data.Convex.UseIfValueExists(v => _meshCollider.convex = v);
			data.IsTrigger.UseIfValueExists(v => _meshCollider.isTrigger = v);
			_physicsMaterial = data.Material.ApplyTo(_meshCollider);
		}

		private void OnDestroy() => PhysicsMaterialProviders.Cleanup(_physicsMaterial);

		public override void Execute(TriggerContext ctx) { }
	}
}
