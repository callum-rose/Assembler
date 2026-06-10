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
		NavPlane Plane,
		float AgentRadius)
	{
		/// <summary>The build-pipeline mapping from the parsed <see cref="NavigationInfo"/> — the single source
		/// of the grid's configuration and defaults.</summary>
		public static NavGridSettings From(NavigationInfo info) =>
			new(info.CellSize, info.MinX, info.MinY, info.MaxX, info.MaxY, info.ObstacleTag, info.Plane,
				info.AgentRadius);

		public static NavGridSettings Default => From(NavigationInfo.Default);
	}

	/// <summary>
	/// Builds and maintains the <see cref="NavGrid"/> for a game and bridges it to the pure pathfinder. The grid
	/// is built lazily on first use by rasterizing every collider on an entity tagged with the obstacle tag onto
	/// the configured <see cref="NavPlane"/> (XY or XZ) — by its actual shape, not its fat bounding box — then
	/// inflating obstacles by the agent radius and caching the result (obstacles are static by default). World
	/// points are projected onto the plane's two axes on the way in and lifted back out on the way out, so the
	/// grid and pathfinder never see the third axis. Exposes world-space A* paths and a small LRU of flow fields
	/// keyed by goal cell for the shared-goal case.
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

		/// <summary>Whether the cell containing <paramref name="world"/> is on the grid and walkable, after
		/// obstacle rasterization and agent-radius inflation.</summary>
		public bool IsWalkable(Vector3 world)
		{
			var grid = Grid();
			var (u, v) = _settings.Plane.Project(world);
			return grid.IsWalkable(grid.WorldToCell(u, v));
		}

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

			// Grow obstacles by the agent radius so paths keep clearance (no-op at the default radius of 0).
			grid.Inflate(_settings.AgentRadius);
			return grid;
		}

		private void RasterizeObstacles(NavGrid grid)
		{
			var obstacles = Object.FindObjectsByType<GameEntity>(FindObjectsSortMode.None)
				.Where(entity => entity.Tags.Contains(_settings.ObstacleTag))
				.SelectMany(entity => entity.GetComponentsInChildren<Collider>());

			foreach (var collider in obstacles)
			{
				BlockCollider(grid, collider);
			}
		}

		// Marks the cells a single obstacle collider actually overlaps unwalkable. Axis-aligned boxes (whose
		// bounds equal their shape) and concave meshes (which Collider.ClosestPoint can't test) take the cheap
		// bounds path; every other convex collider — sphere, capsule, rotated box, convex mesh — is tested per
		// candidate cell against the real shape, so its silhouette blocks the grid rather than its fat AABB.
		// Only convex hulls / AABBs are honoured for concave meshes (a documented simplification).
		private void BlockCollider(NavGrid grid, Collider collider)
		{
			var plane = _settings.Plane;
			var bounds = collider.bounds;
			var (minU, minV) = plane.Project(bounds.min);
			var (maxU, maxV) = plane.Project(bounds.max);

			if (!grid.OverlapsWorldRect(minU, minV, maxU, maxV))
			{
				return;
			}

			if (collider is MeshCollider { convex: false } || (collider is BoxCollider && IsAxisAligned(collider.transform)))
			{
				grid.BlockWorldRect(minU, minV, maxU, maxV);
				return;
			}

			var min = grid.WorldToCell(minU, minV);
			var max = grid.WorldToCell(maxU, maxV);
			var halfCell = grid.CellSize * 0.5f;
			// A hair of slack so a surface grazing a cell edge still counts as overlapping despite float error.
			var reach = halfCell + grid.CellSize * 1e-3f;

			for (var y = min.Y; y <= max.Y; y++)
			{
				for (var x = min.X; x <= max.X; x++)
				{
					var cell = new GridCoord(x, y);
					var (cu, cv) = grid.CellToWorld(cell);
					// Sample at the collider's off-plane centre — the widest cross-section of the supported
					// symmetric shapes — so the in-plane test sees the shape's true footprint at that slice.
					var probe = plane.ToWorld(cu, cv, bounds.center);
					var (clu, clv) = plane.Project(collider.ClosestPoint(probe));

					// The collider reaches this cell iff its nearest point to the cell centre lands within the
					// cell's in-plane rect. A point already inside the collider returns itself, so interior cells
					// block too.
					if (Mathf.Abs(clu - cu) <= reach && Mathf.Abs(clv - cv) <= reach)
					{
						grid.SetWalkable(cell, false);
					}
				}
			}
		}

		// Whether the transform's rotation leaves a box's local axes aligned with the world axes (any 90°
		// multiple), so its world AABB equals its box shape and the exact bounds path is safe. Conservative: any
		// off-axis rotation returns false and falls through to the per-cell shape test.
		private static bool IsAxisAligned(Transform transform)
		{
			var rotation = transform.rotation;
			var snapped = Quaternion.Euler(
				Mathf.Round(rotation.eulerAngles.x / 90f) * 90f,
				Mathf.Round(rotation.eulerAngles.y / 90f) * 90f,
				Mathf.Round(rotation.eulerAngles.z / 90f) * 90f);
			return Quaternion.Angle(rotation, snapped) < 0.01f;
		}
	}
}
