using System;

namespace Assembler.Navigation
{
	/// <summary>
	/// A precomputed flow field over a <see cref="NavGrid"/>: a Dijkstra cost-to-goal for every walkable cell
	/// plus a per-cell unit direction stepping down that cost toward the goal. Cheaper and smoother than running
	/// A* per agent for the common many-agents-one-goal case — every agent just reads its cell's direction.
	/// Deterministic: the cost field is order-independent and direction ties break by neighbour order.
	/// </summary>
	public sealed class FlowField
	{
		private const int Unreachable = int.MaxValue;

		private readonly NavGrid _grid;
		private readonly int[] _cost;
		private readonly float[] _dirX;
		private readonly float[] _dirY;

		private FlowField(NavGrid grid, int[] cost, float[] dirX, float[] dirY, GridCoord goal)
		{
			_grid = grid;
			_cost = cost;
			_dirX = dirX;
			_dirY = dirY;
			Goal = goal;
		}

		public GridCoord Goal { get; }

		public static FlowField Build(NavGrid grid, GridCoord goal, bool allowDiagonal = true)
		{
			var count = grid.Width * grid.Height;
			var cost = new int[count];
			Array.Fill(cost, Unreachable);

			if (grid.IsWalkable(goal))
			{
				Dijkstra(grid, goal, cost, allowDiagonal);
			}

			var dirX = new float[count];
			var dirY = new float[count];
			ComputeDirections(grid, cost, dirX, dirY, allowDiagonal);

			return new FlowField(grid, cost, dirX, dirY, goal);
		}

		/// <summary>True if the cell can reach the goal.</summary>
		public bool HasPath(GridCoord cell) => _grid.InBounds(cell) && _cost[_grid.Index(cell)] != Unreachable;

		/// <summary>Unit step direction from a cell toward the goal, or (0, 0) at the goal / for unreachable cells.</summary>
		public (float X, float Y) Direction(GridCoord cell)
		{
			if (!_grid.InBounds(cell))
			{
				return (0f, 0f);
			}

			var index = _grid.Index(cell);
			return (_dirX[index], _dirY[index]);
		}

		private static void Dijkstra(NavGrid grid, GridCoord goal, int[] cost, bool allowDiagonal)
		{
			var neighbours = GridConnectivity.NeighboursFor(allowDiagonal);
			cost[grid.Index(goal)] = 0;

			// Lowest cost wins; ties break by cell index for a stable order. (The cost field is
			// order-independent regardless, but a total order keeps the search itself deterministic.)
			var frontier = new BinaryHeap<(int Cost, int Index, GridCoord Cell)>((a, b) =>
				a.Cost != b.Cost ? a.Cost.CompareTo(b.Cost) : a.Index.CompareTo(b.Index));

			frontier.Push((0, grid.Index(goal), goal));

			while (frontier.Count > 0)
			{
				var (currentCost, currentIndex, current) = frontier.Pop();

				// Lazy deletion: skip a stale heap entry whose cost has since been beaten.
				if (currentCost > cost[currentIndex])
				{
					continue;
				}

				foreach (var (dx, dy) in neighbours)
				{
					var neighbour = new GridCoord(current.X + dx, current.Y + dy);

					if (!grid.IsWalkable(neighbour) || !GridConnectivity.StepAllowed(grid, current, dx, dy))
					{
						continue;
					}

					var next = currentCost + GridConnectivity.StepCost(dx, dy);
					var neighbourIndex = grid.Index(neighbour);

					if (next < cost[neighbourIndex])
					{
						cost[neighbourIndex] = next;
						frontier.Push((next, neighbourIndex, neighbour));
					}
				}
			}
		}

		private static void ComputeDirections(NavGrid grid, int[] cost, float[] dirX, float[] dirY, bool allowDiagonal)
		{
			var neighbours = GridConnectivity.NeighboursFor(allowDiagonal);

			for (var y = 0; y < grid.Height; y++)
			{
				for (var x = 0; x < grid.Width; x++)
				{
					var cell = new GridCoord(x, y);
					var index = grid.Index(cell);

					if (cost[index] == Unreachable || cost[index] == 0)
					{
						continue;
					}

					var bestCost = cost[index];
					var bestDx = 0;
					var bestDy = 0;

					foreach (var (dx, dy) in neighbours)
					{
						var neighbour = new GridCoord(cell.X + dx, cell.Y + dy);

						if (!grid.IsWalkable(neighbour) || !GridConnectivity.StepAllowed(grid, cell, dx, dy))
						{
							continue;
						}

						var neighbourCost = cost[grid.Index(neighbour)];

						if (neighbourCost < bestCost)
						{
							bestCost = neighbourCost;
							bestDx = dx;
							bestDy = dy;
						}
					}

					if (bestDx == 0 && bestDy == 0)
					{
						continue;
					}

					var length = (float)Math.Sqrt(bestDx * bestDx + bestDy * bestDy);
					dirX[index] = bestDx / length;
					dirY[index] = bestDy / length;
				}
			}
		}
	}
}
