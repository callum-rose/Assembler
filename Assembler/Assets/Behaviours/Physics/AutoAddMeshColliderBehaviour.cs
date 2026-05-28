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
	/// </remarks>
	public sealed class AutoAddMeshColliderBehaviour : GameBehaviour<MeshColliderData>
	{
		private MeshCollider _meshCollider;

		protected override void OnInitialise(MeshColliderData data)
		{
			_meshCollider = gameObject.AddComponent<MeshCollider>();
			data.Convex.UseIfValueExists(v => _meshCollider.convex = v);
			data.IsTrigger.UseIfValueExists(v => _meshCollider.isTrigger = v);
		}

		public override void Execute() { }
	}
}
