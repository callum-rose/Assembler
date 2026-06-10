using System;
using System.Collections.Generic;

namespace Assembler.Navigation
{
	/// <summary>
	/// Deterministic A* over a <see cref="NavGrid"/>. Eight-connected with integer costs (10 orthogonal, 14
	/// diagonal) and an octile heuristic; diagonal moves through a blocked corner are disallowed. The open set
	/// is a binary heap (<see cref="BinaryHeap{T}"/>) ordered by lowest <c>(f, h, cell-index)</c>, so the same
	/// grid + endpoints always yield the identical path — the determinism the project targets for pure
	/// algorithms — while keeping frontier selection O(log n) rather than a linear scan.
	/// </summary>
	public static class AStar
	{
		private readonly struct Node
		{
			public Node(GridCoord cell, int f, int h, int index)
			{
				Cell = cell;
				F = f;
				H = h;
				Index = index;
			}

			public GridCoord Cell { get; }
			public int F { get; }
			public int H { get; }
			public int Index { get; }
		}

		// Lowest f wins; ties break by lower h (closer to the goal), then by cell index. Cell index is unique
		// per cell, so this is a strict total order — the heap result is fully deterministic regardless of
		// insertion order.
		private static readonly Comparison<Node> ByPriority = (a, b) =>
			a.F != b.F ? a.F.CompareTo(b.F) :
			a.H != b.H ? a.H.CompareTo(b.H) :
			a.Index.CompareTo(b.Index);

		/// <summary>
		/// Shortest cell path from <paramref name="start"/> to <paramref name="goal"/> inclusive, or an empty
		/// list if either endpoint is unwalkable/out of bounds or no route exists.
		/// </summary>
		public static IReadOnlyList<GridCoord> FindPath(NavGrid grid, GridCoord start, GridCoord goal,
			bool allowDiagonal = true)
		{
			if (!grid.IsWalkable(start) || !grid.IsWalkable(goal))
			{
				return Array.Empty<GridCoord>();
			}

			if (start.Equals(goal))
			{
				return new[] { goal };
			}

			var neighbours = GridConnectivity.NeighboursFor(allowDiagonal);
			var gScore = new Dictionary<GridCoord, int> { [start] = 0 };
			var cameFrom = new Dictionary<GridCoord, GridCoord>();
			var open = new BinaryHeap<Node>(ByPriority);
			var closed = new HashSet<GridCoord>();

			var startH = Heuristic(start, goal, allowDiagonal);
			open.Push(new Node(start, startH, startH, grid.Index(start)));

			while (open.Count > 0)
			{
				var current = open.Pop().Cell;

				if (current.Equals(goal))
				{
					return Reconstruct(cameFrom, current);
				}

				// Lazy deletion: a cell can sit in the heap more than once (re-pushed when its g improved). The
				// heuristic is consistent, so the first pop of a cell is already optimal; later pops are
				// stale and skipped.
				if (!closed.Add(current))
				{
					continue;
				}

				foreach (var (dx, dy) in neighbours)
				{
					var neighbour = new GridCoord(current.X + dx, current.Y + dy);

					if (!grid.IsWalkable(neighbour) || closed.Contains(neighbour) ||
						!GridConnectivity.StepAllowed(grid, current, dx, dy))
					{
						continue;
					}

					var tentative = gScore[current] + GridConnectivity.StepCost(dx, dy);

					if (!gScore.TryGetValue(neighbour, out var existing) || tentative < existing)
					{
						cameFrom[neighbour] = current;
						gScore[neighbour] = tentative;
						var h = Heuristic(neighbour, goal, allowDiagonal);
						open.Push(new Node(neighbour, tentative + h, h, grid.Index(neighbour)));
					}
				}
			}

			return Array.Empty<GridCoord>();
		}

		// Octile distance when diagonals are allowed; Manhattan (the exact four-connected cost) otherwise. Both
		// are admissible and consistent for their connectivity, so the first pop of any cell stays optimal.
		private static int Heuristic(GridCoord a, GridCoord b, bool allowDiagonal)
		{
			var dx = Math.Abs(a.X - b.X);
			var dy = Math.Abs(a.Y - b.Y);

			return allowDiagonal
				? GridConnectivity.OrthogonalCost * (dx + dy) +
				  (GridConnectivity.DiagonalCost - 2 * GridConnectivity.OrthogonalCost) * Math.Min(dx, dy)
				: GridConnectivity.OrthogonalCost * (dx + dy);
		}

		private static IReadOnlyList<GridCoord> Reconstruct(Dictionary<GridCoord, GridCoord> cameFrom, GridCoord current)
		{
			var path = new List<GridCoord> { current };

			while (cameFrom.TryGetValue(current, out var previous))
			{
				current = previous;
				path.Add(current);
			}

			path.Reverse();
			return path;
		}
	}
}
