using System.Collections.Generic;
using System.Linq;
using Assembler.Navigation;
using Assembler.Parsing.Info;
using UnityEngine;

namespace Assembler.Behaviours.AI
{
	/// <summary>Static configuration for the walkability grid, sourced from the descriptor's <c>Navigation:</c>
	/// section. <see cref="AgentRadius"/> is the game-wide <em>default</em> clearance, used by a navigating agent
	/// that doesn't set its own radius.</summary>
	public sealed record NavGridSettings(
		float CellSize,
		float MinX,
		float MinY,
		float MaxX,
		float MaxY,
		string ObstacleTag,
		NavPlane Plane,
		bool AllowDiagonal,
		float AgentRadius)
	{
		/// <summary>The build-pipeline mapping from the parsed <see cref="NavigationInfo"/> — the single source
		/// of the grid's configuration and defaults.</summary>
		public static NavGridSettings From(NavigationInfo info) =>
			new(info.CellSize, info.MinX, info.MinY, info.MaxX, info.MaxY, info.ObstacleTag, info.Plane,
				info.AllowDiagonal, info.AgentRadius);

		public static NavGridSettings Default => From(NavigationInfo.Default);
	}

	/// <summary>
	/// Builds and maintains the <see cref="NavGrid"/> for a game and bridges it to the pure pathfinder. A single
	/// <em>base</em> grid is built lazily on first use by rasterizing every collider on an entity tagged with the
	/// obstacle tag onto the configured <see cref="NavPlane"/> (XY or XZ) — by its actual shape, not its fat
	/// bounding box. Each distinct <em>agent radius</em> then gets its own grid, the base grid inflated by that
	/// radius and cached, so a large enemy and a small player route around obstacles with their own clearance and
	/// can legitimately take different paths. World points are projected onto the plane's two axes on the way in
	/// and lifted back out on the way out, so the grid and pathfinder never see the third axis. Exposes world-space
	/// A* paths and a small LRU of flow fields keyed by (goal cell, agent radius).
	/// </summary>
	public sealed class NavGridService
	{
		// How many distinct (goal, radius) flow fields to keep. The single-goal swarm is the common case (one
		// entry); a few rival goals/radii coexist without the field thrashing, while the bound caps cost/memory.
		private const int MaxCachedFields = 4;

		private readonly NavGridSettings _settings;
		private readonly Dictionary<(GridCoord Goal, int CellRadius), FlowField> _fields = new();
		private readonly List<(GridCoord Goal, int CellRadius)> _fieldOrder = new();

		// The uninflated grid (obstacles rasterized once), plus one inflated copy per distinct cell radius.
		private NavGrid? _baseGrid;
		private readonly Dictionary<int, NavGrid> _gridsByCellRadius = new();

		public NavGridService(NavGridSettings settings) => _settings = settings;

		public float CellSize => _settings.CellSize;

		/// <summary>Whether the cell containing <paramref name="world"/> is on the grid and walkable for an agent
		/// of the given <paramref name="agentRadius"/> (a negative radius inherits the game-wide default). Used by
		/// grid-locked movers to decide whether the next cell in a direction is enterable.</summary>
		public bool IsWalkable(Vector3 world, float agentRadius)
		{
			var grid = GridFor(agentRadius);
			var (u, v) = _settings.Plane.Project(world);
			return grid.IsWalkable(grid.WorldToCell(u, v));
		}

		/// <summary>The world-space centre of the cell containing <paramref name="world"/>, keeping the point's
		/// off-plane coordinate. Snaps a position onto the grid lattice.</summary>
		public Vector3 CellCentre(Vector3 world)
		{
			// Cell geometry is radius-independent — the lattice is the same on every inflated variant.
			var grid = BaseGrid();
			var (u, v) = _settings.Plane.Project(world);
			var (cu, cv) = grid.CellToWorld(grid.WorldToCell(u, v));
			return _settings.Plane.ToWorld(cu, cv, world);
		}

		/// <summary>The dominant in-plane axis of <paramref name="dir"/> as a unit world-space step, or zero for
		/// a zero/ambiguous input. Snaps an arbitrary heading to a single cardinal so a mover never goes
		/// diagonally (a slight x-bias breaks exact ties deterministically).</summary>
		public Vector3 CardinalStep(Vector3 dir)
		{
			var (u, v) = _settings.Plane.Project(dir);

			if (Mathf.Approximately(u, 0f) && Mathf.Approximately(v, 0f))
			{
				return Vector3.zero;
			}

			return Mathf.Abs(u) >= Mathf.Abs(v)
				? _settings.Plane.ToWorldDirection(Mathf.Sign(u), 0f)
				: _settings.Plane.ToWorldDirection(0f, Mathf.Sign(v));
		}

		/// <summary>World-space waypoints from <paramref name="from"/> to <paramref name="to"/> (goal last) for an
		/// agent of the given <paramref name="agentRadius"/> (a negative radius inherits the game-wide default), or
		/// just the goal if no grid path exists.</summary>
		public IReadOnlyList<Vector3> Path(Vector3 from, Vector3 to, float agentRadius)
		{
			var plane = _settings.Plane;
			var grid = GridFor(agentRadius);
			var (fromU, fromV) = plane.Project(from);
			var (toU, toV) = plane.Project(to);
			var cells = AStar.FindPath(grid, grid.WorldToCell(fromU, fromV), grid.WorldToCell(toU, toV),
				_settings.AllowDiagonal);

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

		/// <summary>Unit flow direction from <paramref name="position"/> toward <paramref name="goal"/> for an agent
		/// of the given <paramref name="agentRadius"/> (a negative radius inherits the game-wide default), using a
		/// flow field cached per (goal cell, radius) and rebuilt only when first seen or evicted.</summary>
		public Vector3 FlowDirection(Vector3 position, Vector3 goal, float agentRadius)
		{
			var plane = _settings.Plane;
			var grid = GridFor(agentRadius);
			var (goalU, goalV) = plane.Project(goal);
			var field = FieldFor(grid, grid.WorldToCell(goalU, goalV), CellRadiusOf(agentRadius));

			var (posU, posV) = plane.Project(position);
			var (dx, dy) = field.Direction(grid.WorldToCell(posU, posV));
			return plane.ToWorldDirection(dx, dy);
		}

		// A small LRU of flow fields keyed by (goal cell, agent radius). A single shared goal+radius stays a
		// straight cache hit; a handful of distinct goals/radii coexist without rebuilding the field on every
		// alternating call — the failure mode of a single cached field.
		private FlowField FieldFor(NavGrid grid, GridCoord goalCell, int cellRadius)
		{
			var key = (goalCell, cellRadius);

			if (_fields.TryGetValue(key, out var cached))
			{
				_fieldOrder.Remove(key);
				_fieldOrder.Add(key);
				return cached;
			}

			var field = FlowField.Build(grid, goalCell, _settings.AllowDiagonal);
			_fields[key] = field;
			_fieldOrder.Add(key);

			if (_fieldOrder.Count > MaxCachedFields)
			{
				_fields.Remove(_fieldOrder[0]);
				_fieldOrder.RemoveAt(0);
			}

			return field;
		}

		// The grid an agent of this radius navigates: the base grid for zero clearance, otherwise the base grid
		// inflated by the radius and cached. Radii that round to the same whole-cell inflation share one grid.
		private NavGrid GridFor(float agentRadius)
		{
			var cellRadius = CellRadiusOf(agentRadius);

			if (cellRadius == 0)
			{
				return BaseGrid();
			}

			if (_gridsByCellRadius.TryGetValue(cellRadius, out var cached))
			{
				return cached;
			}

			var inflated = BaseGrid().Clone();
			inflated.Inflate(cellRadius * _settings.CellSize);
			_gridsByCellRadius[cellRadius] = inflated;
			return inflated;
		}

		// The clearance in whole cells: a negative radius inherits the game-wide default, then round up so a
		// fractional radius never under-clears (matching NavGrid.Inflate's own rounding).
		private int CellRadiusOf(float agentRadius)
		{
			var radius = agentRadius < 0f ? _settings.AgentRadius : agentRadius;
			return radius <= 0f ? 0 : Mathf.CeilToInt(radius / _settings.CellSize);
		}

		// Memoised: the uninflated grid is built once on first use and reused as the source for every inflated
		// variant. CreateBaseGrid is a pure factory (no field side effects), so this stays a null-coalescing get.
		private NavGrid BaseGrid() => _baseGrid ??= CreateBaseGrid();

		private NavGrid CreateBaseGrid()
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
