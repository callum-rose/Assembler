using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	/// <summary>
	/// The renderer walk the auto-fitting collider behaviours share: which renderers belong to <i>this</i>
	/// entity's visual, and the axis-aligned box they occupy in the entity's own local space.
	/// </summary>
	/// <remarks>
	/// Two rules here are load-bearing. First, the walk stops at child entities: a child is parented under
	/// its parent's GameObject, so a plain <c>GetComponentsInChildren</c> would fold every descendant
	/// entity's visual into the parent's collider. Second, bounds come from <see cref="Renderer.localBounds"/>
	/// pushed through transform matrices rather than <see cref="Renderer.bounds"/> — world bounds lag the
	/// transform until the physics scene syncs, so at build time (before the first physics step) they can
	/// still read a pre-placement pose. EditMode hides that, because bounds sync on read there; a real run
	/// does not. <c>NavObstacle</c> paid for this lesson already.
	/// </remarks>
	internal static class VisualBounds
	{
		/// <summary>Smallest extent a fitted collider is given on any axis, so a flat visual (a quad, a plane,
		/// a sprite) still yields a collider physics can actually hit rather than a degenerate zero one.</summary>
		public const float MinimumExtent = 0.001f;

		/// <summary>
		/// The renderers making up this entity's own visual, in hierarchy order — the visual behaviours all
		/// place theirs on a child GameObject. Subtrees belonging to child entities are skipped.
		/// </summary>
		public static IReadOnlyList<Renderer> Renderers(Transform entity)
		{
			var found = new List<Renderer>();
			Collect(entity, entity, found);
			return found;
		}

		/// <summary>
		/// The union of <see cref="Renderers"/>' boxes, expressed in <paramref name="entity"/>'s local space.
		/// False when the entity has no visual at all, in which case <paramref name="bounds"/> is meaningless.
		/// </summary>
		public static bool TryLocalBounds(Transform entity, out Bounds bounds)
		{
			bounds = default;

			var renderers = Renderers(entity);
			if (renderers.Count == 0)
			{
				return false;
			}

			var toEntity = entity.worldToLocalMatrix;
			var started = false;

			foreach (var renderer in renderers)
			{
				var matrix = toEntity * renderer.transform.localToWorldMatrix;
				var local = renderer.localBounds;

				// All eight corners, not just the two extremes: a rotated part's box has to grow to contain
				// its corners, and mapping only min/max would miss them.
				for (var corner = 0; corner < 8; corner++)
				{
					var offset = new Vector3(
						(corner & 1) == 0 ? -local.extents.x : local.extents.x,
						(corner & 2) == 0 ? -local.extents.y : local.extents.y,
						(corner & 4) == 0 ? -local.extents.z : local.extents.z);

					var point = matrix.MultiplyPoint3x4(local.center + offset);

					if (started)
					{
						bounds.Encapsulate(point);
					}
					else
					{
						bounds = new Bounds(point, Vector3.zero);
						started = true;
					}
				}
			}

			return true;
		}

		/// <summary>Each component of <paramref name="size"/> raised to at least <see cref="MinimumExtent"/>.</summary>
		public static Vector3 ClampSize(Vector3 size) =>
			new(Mathf.Max(size.x, MinimumExtent),
				Mathf.Max(size.y, MinimumExtent),
				Mathf.Max(size.z, MinimumExtent));

		/// <summary>The radius of the smallest sphere centred on <paramref name="bounds"/>' centre that spans
		/// its longest axis. Deliberately not the enclosing radius (<c>extents.magnitude</c>), which is
		/// absurdly baggy on anything tall or flat.</summary>
		public static float FittedRadius(Bounds bounds) =>
			Mathf.Max(MinimumExtent,
				Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z)));

		/// <summary>The error thrown when a fitting collider finds nothing to fit to — almost always a
		/// descriptor that lists the collider before the visual behaviour that builds the meshes. Worded for
		/// both callers: <c>part colliders</c> always fits, while box/sphere only do so under
		/// <c>Fit: bounds</c>.</summary>
		public static MissingComponentException MissingVisual(string behaviourName) =>
			new($"'{behaviourName}' found no visual on this entity to fit a collider to. Add a 'model', " +
				"'primitive', 'voxel mesh' or 'sprite' behaviour to the same entity and list it before " +
				$"'{behaviourName}', so its meshes exist by the time the collider fits.");

		private static void Collect(Transform root, Transform current, List<Renderer> found)
		{
			// GameEntityFactory adds a GameEntity to every entity root, so one on a descendant marks the start
			// of a child entity — its visual is that entity's business, not this one's.
			if (current != root && current.GetComponent<GameEntity>() != null)
			{
				return;
			}

			if (current.TryGetComponent<Renderer>(out var renderer))
			{
				found.Add(renderer);
			}

			for (var i = 0; i < current.childCount; i++)
			{
				Collect(root, current.GetChild(i), found);
			}
		}
	}
}
