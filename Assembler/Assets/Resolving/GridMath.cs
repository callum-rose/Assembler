using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assembler.Resolving
{
	// First-class grid/tilemap helpers for grid-based games. Registered globally
	// in CompiledExpressionsRegistry so every descriptor expression can call these
	// by bare name (CellToWorld, RowFull, IsOccupied, ...). Cells are carried as
	// Vector3(col, row, 0); grid dimensions/origin are passed explicitly so the
	// helpers stay stateless. All numeric parameters are float so int arguments
	// coerce automatically during overload resolution.
	public static class GridMath
	{
		// World position of a cell, given the world coordinates of cell (0,0).
		public static Vector3 CellToWorld(Vector3 cell, float originX, float originY)
		{
			return new Vector3(cell.x + originX, cell.y + originY, 0f);
		}

		// World position of a cell with a non-unit, square cell size.
		public static Vector3 CellToWorld(Vector3 cell, float originX, float originY, float cellSize)
		{
			return new Vector3(cell.x * cellSize + originX, cell.y * cellSize + originY, 0f);
		}

		// Inverse of the unit-cell CellToWorld.
		public static Vector3 WorldToCell(Vector3 world, float originX, float originY)
		{
			return new Vector3(world.x - originX, world.y - originY, 0f);
		}

		// col in [0, width-1] and row in [0, height-1].
		public static bool InBounds(float col, float row, float width, float height)
		{
			return col >= 0f && col <= width - 1f && row >= 0f && row <= height - 1f;
		}

		// col in [0, width-1] and row >= 0, with no ceiling (pieces may spawn/extend
		// above the top of a well).
		public static bool InBoundsOpenTop(float col, float row, float width)
		{
			return col >= 0f && col <= width - 1f && row >= 0f;
		}

		// True if any occupied cell sits at (col, row).
		public static bool IsOccupied(List<Vector3> occupied, float col, float row)
		{
			return occupied.Any(c => c.x == col && c.y == row);
		}

		// Number of occupied cells in a row.
		public static int CellsInRow(List<Vector3> occupied, float row)
		{
			return occupied.Where(c => c.y == row).Count();
		}

		// True if a row is completely filled.
		public static bool RowFull(List<Vector3> occupied, float row, float width)
		{
			return CellsInRow(occupied, row) >= width;
		}

		// Number of completely filled rows in [0, height-1].
		public static int FullRowCount(List<Vector3> occupied, float width, float height)
		{
			int n = 0;
			for (int r = 0; r < height; r++)
			{
				if (RowFull(occupied, r, width))
				{
					n++;
				}
			}
			return n;
		}

		// Number of completely filled rows strictly below the given row.
		public static int FullRowsBelow(List<Vector3> occupied, float row, float width, float height)
		{
			int n = 0;
			for (int r = 0; r < height; r++)
			{
				if (r < row && RowFull(occupied, r, width))
				{
					n++;
				}
			}
			return n;
		}

		// Cell offset from a cell by (dCol, dRow).
		public static Vector3 NeighbourCell(Vector3 cell, float dCol, float dRow)
		{
			return new Vector3(cell.x + dCol, cell.y + dRow, 0f);
		}

		// Rotate a cell offset clockwise about the origin, `times` quarter-turns
		// ((x, y) -> (y, -x) per turn). Four turns is the identity.
		public static Vector3 RotateCellCW(Vector3 cell, int times)
		{
			float x = cell.x;
			float y = cell.y;
			for (int k = 0; k < times; k++)
			{
				float nx = y;
				float ny = -x;
				x = nx;
				y = ny;
			}
			return new Vector3(x, y, 0f);
		}
	}
}
