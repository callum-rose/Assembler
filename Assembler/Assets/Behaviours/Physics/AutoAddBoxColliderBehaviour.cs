using System.Collections.Generic;
using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	/// <summary>Adds a Unity BoxCollider to the entity — sized to <c>Size</c>, or fitted automatically to the
	/// entity's visual with <c>Fit</c> so a <c>model</c>'s collision matches its shape without hand-tuned
	/// numbers. Required for collision/trigger physics events.</summary>
	/// <remarks>
	/// Fitting reads the meshes a visual behaviour has already built, so a fitting collider must sit on the
	/// same entity as its visual (<c>model</c>, <c>primitive</c>, <c>voxel mesh</c> or <c>sprite</c>) and be
	/// listed <b>after</b> it, or initialisation throws — behaviours initialise in declaration order.
	/// <c>Fit: bounds</c> fits one collider on the entity root, setting both <c>size</c> and <c>center</c>, so
	/// an off-centre visual (a bottom-anchored <c>model</c> part) gets a collider that actually covers it. It
	/// is one-shot: a <c>!var</c>/<c>!expr</c> that later resizes the visual does not re-fit the collider.
	/// <c>Fit: parts</c> instead puts one collider on each visual part's own GameObject, sized from that
	/// part's mesh, forming a compound collider under the entity's Rigidbody — and because each collider is
	/// sized in its part's local space, it tracks a live-bound part size for free. Renderers belonging to
	/// child entities are excluded from both modes; a child entity's collision is its own to declare.
	/// Properties:
	///   Size: Local-space dimensions of the box (x, y, z). Ignored when Fit is set to anything but none.
	///   Fit: Fit the collider to the entity's visual instead of Size — "none" (default, use Size), "bounds" (one collider on the entity, sized and centred on the whole visual) or "parts" (one collider per visual part, each sized to that part). Requires a visual behaviour on the same entity, listed before this one.
	///   IsTrigger: When true the collider fires trigger events (no physical collision response) instead of acting as a solid collider. Applied to every collider when Fit is parts.
	///   Bounciness: Physics-material bounciness 0–1; when set (with any friction property) a PhysicsMaterial is created and assigned.
	///   DynamicFriction: Physics-material friction 0–1 applied while the surfaces are sliding.
	///   StaticFriction: Physics-material friction 0–1 applied while the surfaces are at rest.
	/// </remarks>
	public sealed class AutoAddBoxColliderBehaviour : AddColliderBehaviour<BoxColliderData>
	{
		protected override IReadOnlyList<Collider> CreateColliders(BoxColliderData data) =>
			data.Fit.ValueOr(ColliderFit.None) switch
			{
				ColliderFit.Bounds => new Collider[] { FitToBounds() },
				ColliderFit.Parts => FitToParts(),
				_ => new Collider[] { Authored(data) }
			};

		private Collider Authored(BoxColliderData data)
		{
			var collider = gameObject.AddComponent<BoxCollider>();
			data.Size.UseIfValueExists(v => collider.size = v);
			return collider;
		}

		private Collider FitToBounds()
		{
			if (!VisualBounds.TryLocalBounds(transform, out var bounds))
			{
				throw VisualBounds.MissingVisual("box collider");
			}

			var collider = gameObject.AddComponent<BoxCollider>();
			collider.size = VisualBounds.ClampSize(bounds.size);
			collider.center = bounds.center;
			return collider;
		}

		private IReadOnlyList<Collider> FitToParts()
		{
			var renderers = VisualBounds.Renderers(transform);

			if (renderers.Count == 0)
			{
				throw VisualBounds.MissingVisual("box collider");
			}

			var colliders = new List<Collider>(renderers.Count);

			foreach (var renderer in renderers)
			{
				// The renderer's own local bounds, on the renderer's own GameObject: no matrix needed, because
				// that transform's scale is what turns them into world size.
				var collider = renderer.gameObject.AddComponent<BoxCollider>();
				collider.size = VisualBounds.ClampSize(renderer.localBounds.size);
				collider.center = renderer.localBounds.center;
				colliders.Add(collider);
			}

			return colliders;
		}
	}
}
