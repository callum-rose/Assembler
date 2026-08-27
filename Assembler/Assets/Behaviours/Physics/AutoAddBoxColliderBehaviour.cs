using System.Collections.Generic;
using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	/// <summary>Adds a Unity BoxCollider to the entity, sized to <c>Size</c> — or, with <c>Fit: bounds</c>,
	/// sized and centred on the entity's visual instead of an authored <c>Size</c>. Required for
	/// collision/trigger physics events.</summary>
	/// <remarks>
	/// <c>Fit: bounds</c> reads the meshes a visual behaviour has already built, so it must sit on the same
	/// entity as its visual (<c>model</c>, <c>primitive</c>, <c>voxel mesh</c> or <c>sprite</c>) and be
	/// listed <b>after</b> it, or initialisation throws — behaviours initialise in declaration order. It sets
	/// both <c>size</c> and <c>center</c>, so an off-centre visual (a bottom-anchored <c>model</c> part) gets
	/// a collider that actually covers it. Fitting is one-shot: a <c>!var</c>/<c>!expr</c> that later resizes
	/// the visual does not re-fit the collider. Renderers belonging to child entities are excluded — a child
	/// entity's collision is its own to declare.
	/// For one collider per visual part rather than one around the whole thing, use <c>part colliders</c>,
	/// which shape-matches each part instead of boxing everything.
	/// Properties:
	///   Size: Local-space dimensions of the box (x, y, z). Ignored when Fit is bounds.
	///   Fit: "none" (default) uses the authored Size; "bounds" ignores Size and fits the collider's size and centre to the entity's visual. Fitting requires a visual behaviour on the same entity, listed before this one.
	///   IsTrigger: When true the collider fires trigger events (no physical collision response) instead of acting as a solid collider.
	///   Bounciness: Physics-material bounciness 0–1; when set (with any friction property) a PhysicsMaterial is created and assigned.
	///   DynamicFriction: Physics-material friction 0–1 applied while the surfaces are sliding.
	///   StaticFriction: Physics-material friction 0–1 applied while the surfaces are at rest.
	/// </remarks>
	public sealed class AutoAddBoxColliderBehaviour : AddColliderBehaviour<BoxColliderData>
	{
		protected override IReadOnlyList<Collider> CreateColliders(BoxColliderData data)
		{
			var collider = gameObject.AddComponent<BoxCollider>();

			if (data.Fit.ValueOr(ColliderFit.None) == ColliderFit.Bounds)
			{
				if (!VisualBounds.TryLocalBounds(transform, out var bounds))
				{
					throw VisualBounds.MissingVisual("box collider");
				}

				collider.size = VisualBounds.ClampSize(bounds.size);
				collider.center = bounds.center;
			}
			else
			{
				data.Size.UseIfValueExists(v => collider.size = v);
			}

			return new Collider[] { collider };
		}
	}
}
