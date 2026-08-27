using System.Collections.Generic;
using Assembler.Behaviours.Visual;
using Assembler.Parsing.Info.Behaviours;
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
	/// is not. Wedge, cone and hemisphere get a convex MeshCollider around their own mesh, because no Unity
	/// primitive collider has a sloped or tapered face: a box around a wedge is a box, and a ramp that
	/// cannot be walked up is not a ramp. A renderer built by something other than a shape (a
	/// <c>voxel mesh</c>, a <c>sprite</c>) falls back to a fitted box.
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
				ShapeKind.Sphere => FitSphere(host, bounds),
				ShapeKind.Capsule or ShapeKind.Cylinder => FitCapsule(host, bounds),
				ShapeKind.Wedge or ShapeKind.Cone or ShapeKind.Hemisphere => FitMesh(host, bounds),
				_ => FitBox(host, bounds)
			};
		}

		// ShapeKind.Unknown is what a renderer no shape built reports — a voxel mesh, a sprite — and the box
		// fallback is what we want for those. Unlike UnityEngine.PrimitiveType, it does not have to lie and
		// call them cubes.
		private static ShapeKind Shape(GameObject host) =>
			host.TryGetComponent<PrimitiveShape>(out var marker) ? marker.Shape : ShapeKind.Unknown;

		private static Collider FitBox(GameObject host, Bounds bounds)
		{
			var collider = host.AddComponent<BoxCollider>();
			collider.size = VisualBounds.ClampSize(bounds.size);
			collider.center = bounds.center;
			return collider;
		}

		// The part's own mesh, marked convex so it can sit under a moving Rigidbody (PhysX only supports
		// convex mesh colliders on non-static bodies) — every shape routed here is convex anyway, so the
		// hull is the mesh rather than an approximation of it. Scale comes from the part transform, exactly
		// as it does for the renderer, so a live-bound Size re-fits this collider for free like the others.
		// A mesh that somehow has no MeshFilter falls back to a box rather than throwing mid-build.
		private static Collider FitMesh(GameObject host, Bounds bounds)
		{
			if (!host.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null)
			{
				return FitBox(host, bounds);
			}

			var collider = host.AddComponent<MeshCollider>();
			collider.sharedMesh = filter.sharedMesh;
			collider.convex = true;
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
