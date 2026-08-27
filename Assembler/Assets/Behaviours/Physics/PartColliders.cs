using System.Collections.Generic;
using Assembler.Behaviours.Visual;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	/// <summary>Gives every part of the entity's visual its own collider, shape-matched to that part and
	/// fitted to its mesh — a compound collider under the entity's Rigidbody. Use this when one box or
	/// sphere around the whole thing is too coarse; for that simpler case use <c>box collider</c> or
	/// <c>sphere collider</c> with <c>Fit: bounds</c>.</summary>
	/// <remarks>
	/// This reads the meshes a visual behaviour has already built, so it must sit on the same entity as its
	/// visual (usually <c>model</c>, but <c>primitive</c>, <c>voxel mesh</c> and <c>sprite</c> work too) and
	/// be listed <b>after</b> it, or initialisation throws — behaviours initialise in declaration order.
	/// Each part's collider is chosen by the shape that built it: cube, quad and plane get a BoxCollider,
	/// sphere gets a SphereCollider, and capsule and cylinder get a CapsuleCollider — so a cylinder is
	/// approximated with rounded ends, which is usually what you want for a leg or a barrel and occasionally
	/// is not. A renderer built by something other than a primitive (a <c>voxel mesh</c>, a <c>sprite</c>)
	/// falls back to a fitted box.
	/// Each collider is sized in its own part's local space, so unlike <c>Fit: bounds</c> — which fits once
	/// from the initial values — these re-fit for free when a part's <c>Size</c> is live-bound, because the
	/// part transform's own scale is what turns the mesh-local size into world size.
	/// Renderers belonging to child entities are excluded; a child entity's collision is its own to declare.
	/// Properties:
	///   IsTrigger: When true every part collider fires trigger events (no physical collision response) instead of acting as a solid collider.
	///   Bounciness: Physics-material bounciness 0–1; when set (with any friction property) one PhysicsMaterial is created and shared across every part collider.
	///   DynamicFriction: Physics-material friction 0–1 applied while the surfaces are sliding.
	///   StaticFriction: Physics-material friction 0–1 applied while the surfaces are at rest.
	/// </remarks>
	public sealed class PartColliders : AddColliderBehaviour<PartColliderData>
	{
		protected override IReadOnlyList<Collider> CreateColliders(PartColliderData data)
		{
			var renderers = VisualBounds.Renderers(transform);

			if (renderers.Count == 0)
			{
				throw VisualBounds.MissingVisual("part colliders");
			}

			var colliders = new List<Collider>(renderers.Count);

			foreach (var renderer in renderers)
			{
				colliders.Add(Fit(renderer));
			}

			return colliders;
		}

		// The collider goes on the renderer's own GameObject and is sized straight from its local bounds — no
		// matrix needed, because that transform's scale is what applies the part's authored world size.
		private static Collider Fit(Renderer renderer)
		{
			var bounds = renderer.localBounds;
			var host = renderer.gameObject;

			return Shape(host) switch
			{
				PrimitiveType.Sphere => FitSphere(host, bounds),
				PrimitiveType.Capsule or PrimitiveType.Cylinder => FitCapsule(host, bounds),
				_ => FitBox(host, bounds)
			};
		}

		// PrimitiveType has no "unknown" member, so a renderer with no marker is reported as Cube — which is
		// the box fallback we want for a voxel mesh or a sprite anyway.
		private static PrimitiveType Shape(GameObject host) =>
			host.TryGetComponent<PrimitiveShape>(out var marker) ? marker.Shape : PrimitiveType.Cube;

		private static Collider FitBox(GameObject host, Bounds bounds)
		{
			var collider = host.AddComponent<BoxCollider>();
			collider.size = VisualBounds.ClampSize(bounds.size);
			collider.center = bounds.center;
			return collider;
		}

		private static Collider FitSphere(GameObject host, Bounds bounds)
		{
			var collider = host.AddComponent<SphereCollider>();
			collider.center = bounds.center;
			collider.radius = VisualBounds.FittedRadius(bounds);
			return collider;
		}

		// A capsule is aligned to its longest axis; the radius spans the larger of the two remaining
		// half-extents so the hemispheres still contain the mesh rather than cutting a corner off it.
		private static Collider FitCapsule(GameObject host, Bounds bounds)
		{
			var extents = bounds.extents;

			var direction = extents.x >= extents.y && extents.x >= extents.z ? 0
				: extents.y >= extents.z ? 1
				: 2;

			var (height, radius) = direction switch
			{
				0 => (bounds.size.x, Mathf.Max(extents.y, extents.z)),
				1 => (bounds.size.y, Mathf.Max(extents.x, extents.z)),
				_ => (bounds.size.z, Mathf.Max(extents.x, extents.y))
			};

			var collider = host.AddComponent<CapsuleCollider>();
			collider.center = bounds.center;
			collider.direction = direction;
			collider.height = Mathf.Max(height, VisualBounds.MinimumExtent);
			collider.radius = Mathf.Max(radius, VisualBounds.MinimumExtent);
			return collider;
		}
	}
}
