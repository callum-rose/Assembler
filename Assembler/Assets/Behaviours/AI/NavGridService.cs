using System.Collections.Generic;
using Assembler.Navigation;
using UnityEngine;

namespace Assembler.Behaviours.AI
{
	/// <summary>Static configuration for the walkability grid, sourced from the descriptor's <c>Navigation:</c> section.</summary>
	public sealed record NavGridSettings(
		float CellSize,
		float MinX,
		float MinY,
		float MaxX,
		float MaxY,
		string ObstacleTag)
	{
		public static NavGridSettings Default => new(1f, -50f, -50f, 50f, 50f, "obstacle");
	}

	/// <summary>
	/// Builds and maintains the <see cref="NavGrid"/> for a game and bridges it to the pure pathfinder. The grid
	/// is built lazily on first use by rasterizing the world-space bounds of every collider on an entity tagged
	/// with the obstacle tag onto the ground (XY) plane, then cached — obstacles are static by default. Exposes
	/// world-space A* paths and a cached flow field for the shared-goal case.
	/// </summary>
	public sealed class NavGridService
	{
		private readonly NavGridSettings _settings;

		private NavGrid? _grid;
		private FlowField? _field;
		private GridCoord _fieldGoal;
		private bool _hasField;

		public NavGridService(NavGridSettings settings) => _settings = settings;

		public float CellSize => _settings.CellSize;

		/// <summary>World-space waypoints from <paramref name="from"/> to <paramref name="to"/> (goal last), or
		/// just the goal if no grid path exists.</summary>
		public IReadOnlyList<Vector3> Path(Vector3 from, Vector3 to)
		{
			var grid = Grid();
			var start = grid.WorldToCell(from.x, from.y);
			var goal = grid.WorldToCell(to.x, to.y);
			var cells = AStar.FindPath(grid, start, goal);

			if (cells.Count == 0)
			{
				return new[] { to };
			}

			var waypoints = new Vector3[cells.Count];

			for (var i = 0; i < cells.Count; i++)
			{
				var (x, y) = grid.CellToWorld(cells[i]);
				waypoints[i] = new Vector3(x, y, to.z);
			}

			// Snap the final waypoint to the exact goal so agents arrive precisely rather than at the cell centre.
			waypoints[^1] = to;
			return waypoints;
		}

		/// <summary>Unit flow direction from <paramref name="position"/> toward <paramref name="goal"/>, using a
		/// flow field cached per goal cell (rebuilt only when the goal moves to a new cell).</summary>
		public Vector3 FlowDirection(Vector3 position, Vector3 goal)
		{
			var grid = Grid();
			var goalCell = grid.WorldToCell(goal.x, goal.y);

			if (!_hasField || !_fieldGoal.Equals(goalCell))
			{
				_field = FlowField.Build(grid, goalCell);
				_fieldGoal = goalCell;
				_hasField = true;
			}

			var (dx, dy) = _field!.Direction(grid.WorldToCell(position.x, position.y));
			return new Vector3(dx, dy, 0f);
		}

		private NavGrid Grid()
		{
			if (_grid != null)
			{
				return _grid;
			}

			_grid = NavGrid.Create(_settings.MinX, _settings.MinY, _settings.MaxX, _settings.MaxY, _settings.CellSize);

			if (!string.IsNullOrEmpty(_settings.ObstacleTag))
			{
				RasterizeObstacles(_grid);
			}

			return _grid;
		}

		private void RasterizeObstacles(NavGrid grid)
		{
			foreach (var entity in Object.FindObjectsByType<GameEntity>(FindObjectsSortMode.None))
			{
				if (!HasTag(entity, _settings.ObstacleTag))
				{
					continue;
				}

				foreach (var collider in entity.GetComponentsInChildren<Collider>())
				{
					Rasterize(grid, collider.bounds);
				}
			}
		}

		private static void Rasterize(NavGrid grid, Bounds bounds)
		{
			var min = grid.WorldToCell(bounds.min.x, bounds.min.y);
			var max = grid.WorldToCell(bounds.max.x, bounds.max.y);

			for (var y = min.Y; y <= max.Y; y++)
			{
				for (var x = min.X; x <= max.X; x++)
				{
					grid.SetWalkable(new GridCoord(x, y), false);
				}
			}
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
