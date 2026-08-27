using System.Collections.Generic;
using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	/// <summary>Adds a Unity SphereCollider to the entity, sized to <c>Radius</c> — or, with <c>Fit: bounds</c>,
	/// sized and centred on the entity's visual instead of an authored <c>Radius</c>. Required for
	/// collision/trigger physics events.</summary>
	/// <remarks>
	/// <c>Fit: bounds</c> reads the meshes a visual behaviour has already built, so it must sit on the same
	/// entity as its visual (<c>model</c>, <c>primitive</c>, <c>voxel mesh</c> or <c>sprite</c>) and be
	/// listed <b>after</b> it, or initialisation throws — behaviours initialise in declaration order. The
	/// fitted sphere sits on the visual's bounds centre with a radius spanning its longest half-extent: the
	/// smallest sphere on that centre that covers the longest axis, deliberately not a sphere enclosing the
	/// whole box, which is absurdly baggy on anything tall. A visual that is not roughly ball-shaped is
	/// better served by a <c>box collider</c>. Fitting is one-shot: a <c>!var</c>/<c>!expr</c> that later
	/// resizes the visual does not re-fit the collider. Renderers belonging to child entities are excluded —
	/// a child entity's collision is its own to declare.
	/// For one collider per visual part rather than one around the whole thing, use <c>part colliders</c>.
	/// Properties:
	///   Radius: Local-space radius of the sphere. Ignored when Fit is bounds.
	///   Fit: "none" (default) uses the authored Radius; "bounds" ignores Radius and fits the collider's radius and centre to the entity's visual. Fitting requires a visual behaviour on the same entity, listed before this one.
	///   IsTrigger: When true the collider fires trigger events (no physical collision response) instead of acting as a solid collider.
	///   Bounciness: Physics-material bounciness 0–1; when set (with any friction property) a PhysicsMaterial is created and assigned.
	///   DynamicFriction: Physics-material friction 0–1 applied while the surfaces are sliding.
	///   StaticFriction: Physics-material friction 0–1 applied while the surfaces are at rest.
	/// </remarks>
	public sealed class AutoAddSphereColliderBehaviour : AddColliderBehaviour<SphereColliderData>
	{
		protected override IReadOnlyList<Collider> CreateColliders(SphereColliderData data)
		{
			var collider = gameObject.AddComponent<SphereCollider>();

			if (data.Fit.ValueOr(ColliderFit.None) == ColliderFit.Bounds)
			{
				if (!VisualBounds.TryLocalBounds(transform, out var bounds))
				{
					throw VisualBounds.MissingVisual("sphere collider");
				}

				collider.center = bounds.center;
				collider.radius = VisualBounds.FittedRadius(bounds);
			}
			else
			{
				data.Radius.UseIfValueExists(v => collider.radius = v);
			}

			return new Collider[] { collider };
		}
	}
}
