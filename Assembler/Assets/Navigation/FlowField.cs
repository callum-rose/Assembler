using System;
using System.Collections.Generic;

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
		private const int OrthogonalCost = 10;
		private const int DiagonalCost = 14;
		private const int Unreachable = int.MaxValue;

		private static readonly (int Dx, int Dy)[] Neighbours =
		{
			(1, 0), (-1, 0), (0, 1), (0, -1),
			(1, 1), (1, -1), (-1, 1), (-1, -1)
		};

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

		public static FlowField Build(NavGrid grid, GridCoord goal)
		{
			var count = grid.Width * grid.Height;
			var cost = new int[count];
			Array.Fill(cost, Unreachable);

			if (grid.IsWalkable(goal))
			{
				Dijkstra(grid, goal, cost);
			}

			var dirX = new float[count];
			var dirY = new float[count];
			ComputeDirections(grid, cost, dirX, dirY);

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

		private static void Dijkstra(NavGrid grid, GridCoord goal, int[] cost)
		{
			cost[grid.Index(goal)] = 0;

			// O(n^2) min-extraction is ample for the modest grids ground AI uses; the result is order-independent.
			var frontier = new List<GridCoord> { goal };

			while (frontier.Count > 0)
			{
				var bestIndex = 0;
				for (var i = 1; i < frontier.Count; i++)
				{
					if (cost[grid.Index(frontier[i])] < cost[grid.Index(frontier[bestIndex])])
					{
						bestIndex = i;
					}
				}

				var current = frontier[bestIndex];
				frontier.RemoveAt(bestIndex);
				var currentCost = cost[grid.Index(current)];

				foreach (var (dx, dy) in Neighbours)
				{
					var neighbour = new GridCoord(current.X + dx, current.Y + dy);

					if (!grid.IsWalkable(neighbour) || !StepAllowed(grid, current, dx, dy))
					{
						continue;
					}

					var step = dx != 0 && dy != 0 ? DiagonalCost : OrthogonalCost;
					var next = currentCost + step;
					var neighbourIndex = grid.Index(neighbour);

					if (next < cost[neighbourIndex])
					{
						cost[neighbourIndex] = next;
						frontier.Add(neighbour);
					}
				}
			}
		}

		private static void ComputeDirections(NavGrid grid, int[] cost, float[] dirX, float[] dirY)
		{
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

					foreach (var (dx, dy) in Neighbours)
					{
						var neighbour = new GridCoord(cell.X + dx, cell.Y + dy);

						if (!grid.IsWalkable(neighbour) || !StepAllowed(grid, cell, dx, dy))
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

		private static bool StepAllowed(NavGrid grid, GridCoord from, int dx, int dy) =>
			dx == 0 || dy == 0 ||
			(grid.IsWalkable(new GridCoord(from.X + dx, from.Y)) &&
			 grid.IsWalkable(new GridCoord(from.X, from.Y + dy)));
	}
}
