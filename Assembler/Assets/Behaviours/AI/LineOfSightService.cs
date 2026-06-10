using UnityEngine;

namespace Assembler.Behaviours.AI
{
	/// <summary>
	/// Answers "can A see B?" by casting a ray between two points and checking whether an obstacle blocks it.
	/// An obstacle is any collider whose owning <see cref="GameEntity"/> carries the given obstacle tag, so it
	/// rides the framework's multi-tag system rather than Unity layers — colliders that are not obstacles
	/// (the seer, the target, pickups, …) never block the line.
	///
	/// Colliders are 3D in this project even for 2D games (2D entities live on the z = 0 plane), so a single 3D
	/// raycast serves both dimensionalities. LOS depends on physics and is therefore outside the determinism
	/// guarantee, consistent with the project's stance.
	/// </summary>
	public sealed class LineOfSightService
	{
		/// <summary>
		/// True if nothing tagged <paramref name="obstacleTag"/> lies between <paramref name="from"/> and
		/// <paramref name="to"/>. An empty <paramref name="obstacleTag"/> means "no obstacles" — always visible.
		/// </summary>
		public bool CanSee(Vector3 from, Vector3 to, string obstacleTag)
		{
			if (string.IsNullOrEmpty(obstacleTag))
			{
				return true;
			}

			var offset = to - from;
			var distance = offset.magnitude;

			if (distance <= 1e-4f)
			{
				return true;
			}

			var hits = UnityEngine.Physics.RaycastAll(from, offset / distance, distance);

			foreach (var hit in hits)
			{
				var entity = hit.collider.GetComponentInParent<GameEntity>();

				if (entity != null && HasTag(entity, obstacleTag))
				{
					return false;
				}
			}

			return true;
		}

		private static bool HasTag(GameEntity entity, string tag)
		{
			foreach (var entityTag in entity.Tags)
			{
				if (entityTag == tag)
				{
					return true;
				}
			}

			return false;
		}
	}
}
