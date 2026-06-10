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

		/// <summary>
		/// Marks every cell overlapping the world-space rectangle <c>[minX,maxX] × [minY,maxY]</c> unwalkable.
		/// A rectangle that doesn't overlap the grid at all is ignored, rather than being smeared into a
		/// phantom wall along the nearest edge — <see cref="WorldToCell"/> clamps, so without this guard an
		/// entirely off-grid obstacle would block a boundary row/column.
		/// </summary>
		public void BlockWorldRect(float minX, float minY, float maxX, float maxY)
		{
			if (maxX < OriginX || minX > OriginX + Width * CellSize ||
				maxY < OriginY || minY > OriginY + Height * CellSize)
			{
				return;
			}

			var min = WorldToCell(minX, minY);
			var max = WorldToCell(maxX, maxY);

			for (var y = min.Y; y <= max.Y; y++)
			{
				for (var x = min.X; x <= max.X; x++)
				{
					_walkable[Index(new GridCoord(x, y))] = false;
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
