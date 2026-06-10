using System.Collections.Generic;
using System.Linq;
using Assembler.Navigation;
using Assembler.Parsing.Info;
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
		string ObstacleTag,
		NavPlane Plane)
	{
		/// <summary>The build-pipeline mapping from the parsed <see cref="NavigationInfo"/> — the single source
		/// of the grid's configuration and defaults.</summary>
		public static NavGridSettings From(NavigationInfo info) =>
			new(info.CellSize, info.MinX, info.MinY, info.MaxX, info.MaxY, info.ObstacleTag, info.Plane);

		public static NavGridSettings Default => From(NavigationInfo.Default);
	}

	/// <summary>
	/// Builds and maintains the <see cref="NavGrid"/> for a game and bridges it to the pure pathfinder. The grid
	/// is built lazily on first use by rasterizing the world-space bounds of every collider on an entity tagged
	/// with the obstacle tag onto the configured <see cref="NavPlane"/> (XY or XZ), then cached — obstacles are
	/// static by default. World points are projected onto the plane's two axes on the way in and lifted back
	/// out on the way out, so the grid and pathfinder never see the third axis. Exposes world-space A* paths
	/// and a small LRU of flow fields keyed by goal cell for the shared-goal case.
	/// </summary>
	public sealed class NavGridService
	{
		// How many distinct-goal flow fields to keep. The single-goal swarm is the common case (one entry);
		// a few rival goals coexist without the field thrashing, while the bound caps build cost and memory.
		private const int MaxCachedFields = 4;

		private readonly NavGridSettings _settings;
		private readonly Dictionary<GridCoord, FlowField> _fields = new();
		private readonly List<GridCoord> _fieldOrder = new();

		private NavGrid? _grid;

		public NavGridService(NavGridSettings settings) => _settings = settings;

		public float CellSize => _settings.CellSize;

		/// <summary>World-space waypoints from <paramref name="from"/> to <paramref name="to"/> (goal last), or
		/// just the goal if no grid path exists.</summary>
		public IReadOnlyList<Vector3> Path(Vector3 from, Vector3 to)
		{
			var plane = _settings.Plane;
			var grid = Grid();
			var (fromU, fromV) = plane.Project(from);
			var (toU, toV) = plane.Project(to);
			var cells = AStar.FindPath(grid, grid.WorldToCell(fromU, fromV), grid.WorldToCell(toU, toV));

			if (cells.Count == 0)
			{
				return new[] { to };
			}

			var waypoints = new Vector3[cells.Count];

			for (var i = 0; i < cells.Count; i++)
			{
				var (u, v) = grid.CellToWorld(cells[i]);
				// Keep the goal's off-plane coordinate so waypoints share the target's depth/height.
				waypoints[i] = plane.ToWorld(u, v, to);
			}

			// Snap the final waypoint to the exact goal so agents arrive precisely rather than at the cell centre.
			waypoints[^1] = to;
			return waypoints;
		}

		/// <summary>Unit flow direction from <paramref name="position"/> toward <paramref name="goal"/>, using a
		/// flow field cached per goal cell (rebuilt only when a goal cell is first seen or evicted).</summary>
		public Vector3 FlowDirection(Vector3 position, Vector3 goal)
		{
			var plane = _settings.Plane;
			var grid = Grid();
			var (goalU, goalV) = plane.Project(goal);
			var field = FieldFor(grid, grid.WorldToCell(goalU, goalV));

			var (posU, posV) = plane.Project(position);
			var (dx, dy) = field.Direction(grid.WorldToCell(posU, posV));
			return plane.ToWorldDirection(dx, dy);
		}

		// A small LRU of flow fields keyed by goal cell. A single shared goal stays a straight cache hit;
		// a handful of distinct goals coexist without rebuilding the field on every alternating call — the
		// failure mode of a single cached field.
		private FlowField FieldFor(NavGrid grid, GridCoord goalCell)
		{
			if (_fields.TryGetValue(goalCell, out var cached))
			{
				_fieldOrder.Remove(goalCell);
				_fieldOrder.Add(goalCell);
				return cached;
			}

			var field = FlowField.Build(grid, goalCell);
			_fields[goalCell] = field;
			_fieldOrder.Add(goalCell);

			if (_fieldOrder.Count > MaxCachedFields)
			{
				_fields.Remove(_fieldOrder[0]);
				_fieldOrder.RemoveAt(0);
			}

			return field;
		}

		// Memoised: the grid is built once on first use and reused. CreateGrid is a pure factory (no field
		// side effects), so this stays a plain null-coalescing assignment.
		private NavGrid Grid() => _grid ??= CreateGrid();

		private NavGrid CreateGrid()
		{
			var grid = NavGrid.Create(_settings.MinX, _settings.MinY, _settings.MaxX, _settings.MaxY, _settings.CellSize);

			if (!string.IsNullOrEmpty(_settings.ObstacleTag))
			{
				RasterizeObstacles(grid);
			}

			return grid;
		}

		private void RasterizeObstacles(NavGrid grid)
		{
			var obstacles = Object.FindObjectsByType<GameEntity>(FindObjectsSortMode.None)
				.Where(entity => entity.Tags.Contains(_settings.ObstacleTag))
				.SelectMany(entity => entity.GetComponentsInChildren<Collider>());

			foreach (var collider in obstacles)
			{
				var (minU, minV) = _settings.Plane.Project(collider.bounds.min);
				var (maxU, maxV) = _settings.Plane.Project(collider.bounds.max);
				grid.BlockWorldRect(minU, minV, maxU, maxV);
			}
		}
	}
}
