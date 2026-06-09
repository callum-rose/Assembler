using System;
using System.Collections.Generic;

namespace Assembler.Navigation
{
	/// <summary>
	/// Deterministic A* over a <see cref="NavGrid"/>. Eight-connected with integer costs (10 orthogonal, 14
	/// diagonal) and an octile heuristic; diagonal moves through a blocked corner are disallowed. Ties are
	/// broken by lowest <c>(f, h, cell-index)</c> so the same grid + endpoints always yield the identical path
	/// — the determinism the project targets for pure algorithms.
	/// </summary>
	public static class AStar
	{
		private const int OrthogonalCost = 10;
		private const int DiagonalCost = 14;

		// Deterministic neighbour order (orthogonal first, then diagonals); the open-set selection breaks ties
		// independently, so this order only fixes expansion among otherwise-equal options.
		private static readonly (int Dx, int Dy)[] Neighbours =
		{
			(1, 0), (-1, 0), (0, 1), (0, -1),
			(1, 1), (1, -1), (-1, 1), (-1, -1)
		};

		/// <summary>
		/// Shortest cell path from <paramref name="start"/> to <paramref name="goal"/> inclusive, or an empty
		/// list if either endpoint is unwalkable/out of bounds or no route exists.
		/// </summary>
		public static IReadOnlyList<GridCoord> FindPath(NavGrid grid, GridCoord start, GridCoord goal)
		{
			if (!grid.IsWalkable(start) || !grid.IsWalkable(goal))
			{
				return Array.Empty<GridCoord>();
			}

			if (start.Equals(goal))
			{
				return new[] { goal };
			}

			var gScore = new Dictionary<GridCoord, int> { [start] = 0 };
			var cameFrom = new Dictionary<GridCoord, GridCoord>();
			var open = new List<GridCoord> { start };
			var openSet = new HashSet<GridCoord> { start };
			var closed = new HashSet<GridCoord>();

			while (open.Count > 0)
			{
				var current = SelectBest(open, gScore, goal, grid);

				if (current.Equals(goal))
				{
					return Reconstruct(cameFrom, current);
				}

				open.Remove(current);
				openSet.Remove(current);
				closed.Add(current);

				foreach (var (dx, dy) in Neighbours)
				{
					var neighbour = new GridCoord(current.X + dx, current.Y + dy);

					if (!grid.IsWalkable(neighbour) || closed.Contains(neighbour))
					{
						continue;
					}

					// No corner cutting: a diagonal step requires both shared orthogonal cells to be walkable.
					if (dx != 0 && dy != 0 &&
						(!grid.IsWalkable(new GridCoord(current.X + dx, current.Y)) ||
						 !grid.IsWalkable(new GridCoord(current.X, current.Y + dy))))
					{
						continue;
					}

					var tentative = gScore[current] + (dx != 0 && dy != 0 ? DiagonalCost : OrthogonalCost);

					if (!gScore.TryGetValue(neighbour, out var existing) || tentative < existing)
					{
						cameFrom[neighbour] = current;
						gScore[neighbour] = tentative;

						if (openSet.Add(neighbour))
						{
							open.Add(neighbour);
						}
					}
				}
			}

			return Array.Empty<GridCoord>();
		}

		private static GridCoord SelectBest(List<GridCoord> open, Dictionary<GridCoord, int> gScore, GridCoord goal, NavGrid grid)
		{
			var best = open[0];
			var bestF = gScore[best] + Heuristic(best, goal);
			var bestH = Heuristic(best, goal);
			var bestIndex = grid.Index(best);

			for (var i = 1; i < open.Count; i++)
			{
				var node = open[i];
				var h = Heuristic(node, goal);
				var f = gScore[node] + h;
				var index = grid.Index(node);

				if (f < bestF || (f == bestF && (h < bestH || (h == bestH && index < bestIndex))))
				{
					best = node;
					bestF = f;
					bestH = h;
					bestIndex = index;
				}
			}

			return best;
		}

		private static int Heuristic(GridCoord a, GridCoord b)
		{
			var dx = Math.Abs(a.X - b.X);
			var dy = Math.Abs(a.Y - b.Y);
			// Octile distance with the integer cost scale.
			return OrthogonalCost * (dx + dy) + (DiagonalCost - 2 * OrthogonalCost) * Math.Min(dx, dy);
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
