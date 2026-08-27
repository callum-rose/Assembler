using Assembler.Parsing.Info.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Visual
{
	/// <summary>
	/// Records which <see cref="ShapeKind"/> built this mesh child. <c>model</c> and <c>primitive</c>
	/// stamp it on every mesh they create so <c>part colliders</c> can give each part a collider matching
	/// its shape — a capsule gets a CapsuleCollider, not a box.
	/// </summary>
	/// <remarks>
	/// A marker rather than sniffing <c>MeshFilter.sharedMesh.name</c>: a mesh name is not a contract, and
	/// a silently mis-shaped collider is exactly the failure this is meant to prevent. A renderer with no
	/// marker (a <c>voxel mesh</c>, a <c>sprite</c>) reports <see cref="ShapeKind.Unknown"/> and falls back
	/// to a fitted box.
	/// </remarks>
	public sealed class PrimitiveShape : MonoBehaviour
	{
		public ShapeKind Shape { get; set; }
	}
}
