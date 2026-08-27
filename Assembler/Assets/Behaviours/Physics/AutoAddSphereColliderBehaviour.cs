using System.Collections.Generic;
using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	/// <summary>Adds a Unity SphereCollider to the entity — sized to <c>Radius</c>, or fitted automatically to
	/// the entity's visual with <c>Fit</c>. Required for collision/trigger physics events.</summary>
	/// <remarks>
	/// Fitting reads the meshes a visual behaviour has already built, so a fitting collider must sit on the
	/// same entity as its visual (<c>model</c>, <c>primitive</c>, <c>voxel mesh</c> or <c>sprite</c>) and be
	/// listed <b>after</b> it, or initialisation throws — behaviours initialise in declaration order.
	/// <c>Fit: bounds</c> puts one collider on the entity root, centred on the visual's bounds with a radius
	/// spanning its longest half-extent — the smallest sphere on that centre that covers the longest axis, not
	/// a sphere enclosing the whole box, which is absurdly baggy on anything tall. A visual that is not
	/// roughly ball-shaped is better served by a <c>box collider</c>. Fitting is one-shot: a
	/// <c>!var</c>/<c>!expr</c> that later resizes the visual does not re-fit the collider.
	/// <c>Fit: parts</c> instead puts one collider on each visual part's own GameObject, sized from that
	/// part's mesh, forming a compound collider under the entity's Rigidbody — and because each collider is
	/// sized in its part's local space, it tracks a live-bound part size for free. Renderers belonging to
	/// child entities are excluded from both modes; a child entity's collision is its own to declare.
	/// Properties:
	///   Radius: Local-space radius of the sphere. Ignored when Fit is set to anything but none.
	///   Fit: Fit the collider to the entity's visual instead of Radius — "none" (default, use Radius), "bounds" (one collider on the entity, centred on the whole visual with a radius spanning its longest half-extent) or "parts" (one collider per visual part, each fitted to that part). Requires a visual behaviour on the same entity, listed before this one.
	///   IsTrigger: When true the collider fires trigger events (no physical collision response) instead of acting as a solid collider. Applied to every collider when Fit is parts.
	///   Bounciness: Physics-material bounciness 0–1; when set (with any friction property) a PhysicsMaterial is created and assigned.
	///   DynamicFriction: Physics-material friction 0–1 applied while the surfaces are sliding.
	///   StaticFriction: Physics-material friction 0–1 applied while the surfaces are at rest.
	/// </remarks>
	public sealed class AutoAddSphereColliderBehaviour : AddColliderBehaviour<SphereColliderData>
	{
		protected override IReadOnlyList<Collider> CreateColliders(SphereColliderData data) =>
			data.Fit.ValueOr(ColliderFit.None) switch
			{
				ColliderFit.Bounds => new Collider[] { FitToBounds() },
				ColliderFit.Parts => FitToParts(),
				_ => new Collider[] { Authored(data) }
			};

		private Collider Authored(SphereColliderData data)
		{
			var collider = gameObject.AddComponent<SphereCollider>();
			data.Radius.UseIfValueExists(v => collider.radius = v);
			return collider;
		}

		private Collider FitToBounds()
		{
			if (!VisualBounds.TryLocalBounds(transform, out var bounds))
			{
				throw VisualBounds.MissingVisual("sphere collider");
			}

			var collider = gameObject.AddComponent<SphereCollider>();
			collider.center = bounds.center;
			collider.radius = VisualBounds.FittedRadius(bounds);
			return collider;
		}

		private IReadOnlyList<Collider> FitToParts()
		{
			var renderers = VisualBounds.Renderers(transform);

			if (renderers.Count == 0)
			{
				throw VisualBounds.MissingVisual("sphere collider");
			}

			var colliders = new List<Collider>(renderers.Count);

			foreach (var renderer in renderers)
			{
				// The renderer's own local bounds, on the renderer's own GameObject: no matrix needed, because
				// that transform's scale is what turns them into world size.
				var collider = renderer.gameObject.AddComponent<SphereCollider>();
				collider.center = renderer.localBounds.center;
				collider.radius = VisualBounds.FittedRadius(renderer.localBounds);
				colliders.Add(collider);
			}

			return colliders;
		}
	}
}
