using System;
using System.Collections.Generic;

namespace Assembler.Navigation
{
	/// <summary>
	/// Shared grid-search primitives for <see cref="AStar"/> and <see cref="FlowField"/>: the eight-connected
	/// neighbour offsets, the integer move costs, and the no-corner-cutting rule. Centralised so the two
	/// searches can never drift apart on connectivity or cost.
	/// </summary>
	internal static class GridConnectivity
	{
		public const int OrthogonalCost = 10;
		public const int DiagonalCost = 14;

		// Orthogonal first, then diagonals; a fixed order so any tie-breaking among otherwise-equal options is
		// deterministic.
		public static readonly (int Dx, int Dy)[] Neighbours =
		{
			(1, 0), (-1, 0), (0, 1), (0, -1),
			(1, 1), (1, -1), (-1, 1), (-1, -1)
		};

		// The orthogonal subset, for four-connected searches that must never step diagonally (e.g. a maze whose
		// agents move on the grid, Pacman-style). Same fixed order as the first four of <see cref="Neighbours"/>.
		public static readonly (int Dx, int Dy)[] OrthogonalNeighbours =
		{
			(1, 0), (-1, 0), (0, 1), (0, -1)
		};

		/// <summary>The neighbour offsets for a search: all eight when diagonals are allowed, the four
		/// orthogonal ones otherwise.</summary>
		public static (int Dx, int Dy)[] NeighboursFor(bool allowDiagonal) =>
			allowDiagonal ? Neighbours : OrthogonalNeighbours;

		public static int StepCost(int dx, int dy) => dx != 0 && dy != 0 ? DiagonalCost : OrthogonalCost;

		/// <summary>A diagonal step is allowed only when both shared orthogonal cells are walkable (no cutting
		/// through a blocked corner); orthogonal steps are always allowed.</summary>
		public static bool StepAllowed(NavGrid grid, GridCoord from, int dx, int dy) =>
			dx == 0 || dy == 0 ||
			(grid.IsWalkable(new GridCoord(from.X + dx, from.Y)) &&
			 grid.IsWalkable(new GridCoord(from.X, from.Y + dy)));
	}

	/// <summary>
	/// A minimal binary min-heap used as the open set / frontier for the grid searches, giving O(log n) push
	/// and pop in place of a linear scan over the frontier. Ordering is supplied by a <see cref="Comparison{T}"/>
	/// so each caller encodes its own deterministic tie-break.
	/// </summary>
	internal sealed class BinaryHeap<T>
	{
		private readonly List<T> _items = new();
		private readonly Comparison<T> _compare;

		public BinaryHeap(Comparison<T> compare) => _compare = compare;

		public int Count => _items.Count;

		public void Push(T item)
		{
			_items.Add(item);

			var child = _items.Count - 1;
			while (child > 0)
			{
				var parent = (child - 1) / 2;
				if (_compare(_items[child], _items[parent]) >= 0)
				{
					break;
				}

				(_items[child], _items[parent]) = (_items[parent], _items[child]);
				child = parent;
			}
		}

		public T Pop()
		{
			var root = _items[0];
			var last = _items.Count - 1;
			_items[0] = _items[last];
			_items.RemoveAt(last);
			last--;

			var parent = 0;
			while (true)
			{
				var left = 2 * parent + 1;
				var right = 2 * parent + 2;
				var smallest = parent;

				if (left <= last && _compare(_items[left], _items[smallest]) < 0)
				{
					smallest = left;
				}

				if (right <= last && _compare(_items[right], _items[smallest]) < 0)
				{
					smallest = right;
				}

				if (smallest == parent)
				{
					break;
				}

				(_items[parent], _items[smallest]) = (_items[smallest], _items[parent]);
				parent = smallest;
			}

			return root;
		}
	}
}
