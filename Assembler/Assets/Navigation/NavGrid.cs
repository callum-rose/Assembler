using System;

namespace Assembler.Navigation
{
	/// <summary>Integer grid coordinate (column, row).</summary>
	public readonly struct GridCoord : IEquatable<GridCoord>
	{
		public int X { get; }
		public int Y { get; }

		public GridCoord(int x, int y)
		{
			X = x;
			Y = y;
		}

		public bool Equals(GridCoord other) => X == other.X && Y == other.Y;

		public override bool Equals(object? obj) => obj is GridCoord other && Equals(other);

		public override int GetHashCode() => unchecked((X * 397) ^ Y);

		public override string ToString() => $"({X}, {Y})";
	}

	/// <summary>
	/// A pure, Unity-free walkability grid: an origin (the grid's min corner in world units), a uniform cell
	/// size, dimensions, and a row-major <c>bool[]</c> of which cells are walkable. It is the shared model the
	/// deterministic pathfinder (<see cref="AStar"/>) and flow field (<see cref="FlowField"/>) operate on, and
	/// the bridge a Unity nav service rasterizes obstacles into. World ↔ cell conversion uses cell centres.
	/// </summary>
	public sealed class NavGrid
	{
		private readonly bool[] _walkable;

		public NavGrid(float originX, float originY, float cellSize, int width, int height, bool[] walkable)
		{
			if (cellSize <= 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be positive.");
			}

			if (width <= 0 || height <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(width), "Grid dimensions must be positive.");
			}

			if (walkable.Length != width * height)
			{
				throw new ArgumentException(
					$"Walkable array length {walkable.Length} does not match {width}x{height} = {width * height}.",
					nameof(walkable));
			}

			OriginX = originX;
			OriginY = originY;
			CellSize = cellSize;
			Width = width;
			Height = height;
			_walkable = walkable;
		}

		public float OriginX { get; }
		public float OriginY { get; }
		public float CellSize { get; }
		public int Width { get; }
		public int Height { get; }

		/// <summary>Builds an all-walkable grid spanning the given world bounds at the given cell size.</summary>
		public static NavGrid Create(float minX, float minY, float maxX, float maxY, float cellSize)
		{
			var width = Math.Max(1, (int)Math.Ceiling((maxX - minX) / cellSize));
			var height = Math.Max(1, (int)Math.Ceiling((maxY - minY) / cellSize));
			var walkable = new bool[width * height];
			Array.Fill(walkable, true);
			return new NavGrid(minX, minY, cellSize, width, height, walkable);
		}

		public int Index(GridCoord cell) => cell.Y * Width + cell.X;

		public bool InBounds(GridCoord cell) =>
			cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;

		public bool IsWalkable(GridCoord cell) => InBounds(cell) && _walkable[Index(cell)];

		public void SetWalkable(GridCoord cell, bool walkable)
		{
			if (InBounds(cell))
			{
				_walkable[Index(cell)] = walkable;
			}
		}

		/// <summary>Whether the world-space rectangle <c>[minX,maxX] × [minY,maxY]</c> overlaps the grid at all.
		/// The off-grid guard <see cref="BlockWorldRect"/> uses (and the per-cell obstacle rasterizer reuses) so
		/// an obstacle entirely outside the grid is skipped rather than smeared into a boundary row/column —
		/// <see cref="WorldToCell"/> clamps, so without this an off-grid rect would block the nearest edge.</summary>
		public bool OverlapsWorldRect(float minX, float minY, float maxX, float maxY) =>
			maxX >= OriginX && minX <= OriginX + Width * CellSize &&
			maxY >= OriginY && minY <= OriginY + Height * CellSize;

		/// <summary>
		/// Marks every cell overlapping the world-space rectangle <c>[minX,maxX] × [minY,maxY]</c> unwalkable.
		/// A rectangle that doesn't overlap the grid at all is ignored (see <see cref="OverlapsWorldRect"/>),
		/// rather than being smeared into a phantom wall along the nearest edge.
		/// The max edge is treated as half-open: a face flush with a cell boundary blocks the cell it covers,
		/// not the next cell over. Without this, a wall whose far face lands exactly on a boundary (the common
		/// case when cell centres are integers and walls are integer-sized) would over-block one extra row/
		/// column of open cells — silently swallowing dots, turns and spawn cells in a grid maze.
		/// </summary>
		public void BlockWorldRect(float minX, float minY, float maxX, float maxY)
		{
			if (!OverlapsWorldRect(minX, minY, maxX, maxY))
			{
				return;
			}

			// Nudge the max corner inward by a hair so a face exactly on a cell boundary rounds down into the
			// wall's own cell. The nudge is far smaller than a cell, so it only changes the flush-boundary case.
			var nudge = CellSize * 1e-3f;
			var min = WorldToCell(minX, minY);
			var max = WorldToCell(maxX - nudge, maxY - nudge);

			// A wall thinner than the nudge could invert the range; never block fewer cells than the min corner.
			var maxX2 = Math.Max(min.X, max.X);
			var maxY2 = Math.Max(min.Y, max.Y);

			for (var y = min.Y; y <= maxY2; y++)
			{
				for (var x = min.X; x <= maxX2; x++)
				{
					_walkable[Index(new GridCoord(x, y))] = false;
				}
			}
		}

		/// <summary>
		/// Grows every blocked region outward by <paramref name="radius"/> world units so paths keep that much
		/// clearance from obstacles — modelling an agent of that radius as a point on the inflated grid. Each
		/// originally-blocked cell stamps a Euclidean disk of cells unwalkable; the radius is rounded up to whole
		/// cells, erring toward more clearance. A non-positive radius is a no-op. Intended to run once at build,
		/// after rasterization.
		/// </summary>
		public void Inflate(float radius)
		{
			var cellRadius = (int)Math.Ceiling(radius / CellSize);

			if (cellRadius <= 0)
			{
				return;
			}

			// Snapshot the pre-inflation walls so the stamp grows from the original obstacles only, not from
			// cells it has itself just filled in (which would let clearance bleed outward without bound).
			var original = (bool[])_walkable.Clone();

			for (var cy = 0; cy < Height; cy++)
			{
				for (var cx = 0; cx < Width; cx++)
				{
					if (original[Index(new GridCoord(cx, cy))])
					{
						continue;
					}

					for (var dy = -cellRadius; dy <= cellRadius; dy++)
					{
						for (var dx = -cellRadius; dx <= cellRadius; dx++)
						{
							if (dx * dx + dy * dy <= cellRadius * cellRadius)
							{
								SetWalkable(new GridCoord(cx + dx, cy + dy), false);
							}
						}
					}
				}
			}
		}

		/// <summary>Nearest cell containing a world point, clamped into bounds.</summary>
		public GridCoord WorldToCell(float worldX, float worldY)
		{
			var x = (int)Math.Floor((worldX - OriginX) / CellSize);
			var y = (int)Math.Floor((worldY - OriginY) / CellSize);
			return new GridCoord(Clamp(x, 0, Width - 1), Clamp(y, 0, Height - 1));
		}

		/// <summary>World-space centre of a cell.</summary>
		public (float X, float Y) CellToWorld(GridCoord cell) =>
			(OriginX + (cell.X + 0.5f) * CellSize, OriginY + (cell.Y + 0.5f) * CellSize);

		private static int Clamp(int value, int min, int max) =>
			value < min ? min : value > max ? max : value;
	}
}
