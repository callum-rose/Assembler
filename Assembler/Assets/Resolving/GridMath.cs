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
		public static Vector3 CellToWorld(Vector3 cell, float originX, float originY) =>
			new(cell.x + originX, cell.y + originY, 0f);

		// World position of a cell with a non-unit, square cell size.
		public static Vector3 CellToWorld(Vector3 cell, float originX, float originY, float cellSize) =>
			new(cell.x * cellSize + originX, cell.y * cellSize + originY, 0f);

		// Inverse of the unit-cell CellToWorld.
		public static Vector3 WorldToCell(Vector3 world, float originX, float originY) =>
			new(world.x - originX, world.y - originY, 0f);

		// col in [0, width-1] and row in [0, height-1].
		public static bool InBounds(float col, float row, float width, float height) =>
			col >= 0f && col <= width - 1f && row >= 0f && row <= height - 1f;

		// col in [0, width-1] and row >= 0, with no ceiling (pieces may spawn/extend
		// above the top of a well).
		public static bool InBoundsOpenTop(float col, float row, float width) =>
			col >= 0f && col <= width - 1f && row >= 0f;

		// True if any occupied cell sits at (col, row).
		public static bool IsOccupied(List<Vector3> occupied, float col, float row) =>
			occupied.Any(c => c.x == col && c.y == row);

		// Number of occupied cells in a row.
		public static int CellsInRow(List<Vector3> occupied, float row) =>
			occupied.Where(c => c.y == row).Count();

		// True if a row is completely filled.
		public static bool RowFull(List<Vector3> occupied, float row, float width) =>
			CellsInRow(occupied, row) >= width;

		// Number of completely filled rows in [0, height-1].
		public static int FullRowCount(List<Vector3> occupied, float width, float height) =>
			Enumerable.Range(0, (int)height).Count(r => RowFull(occupied, r, width));

		// Number of completely filled rows strictly below the given row.
		public static int FullRowsBelow(List<Vector3> occupied, float row, float width, float height) =>
			Enumerable.Range(0, (int)height).Count(r => r < row && RowFull(occupied, r, width));

		// Cell offset from a cell by (dCol, dRow).
		public static Vector3 NeighbourCell(Vector3 cell, float dCol, float dRow) =>
			new(cell.x + dCol, cell.y + dRow, 0f);

		// Rotate a cell offset clockwise about the origin, `times` quarter-turns
		// ((x, y) -> (y, -x) per turn). Four turns is the identity.
		public static Vector3 RotateCellCW(Vector3 cell, int times) => (times % 4 + 4) % 4 switch
		{
			1 => new Vector3(cell.y, -cell.x, 0f),
			2 => new Vector3(-cell.x, -cell.y, 0f),
			3 => new Vector3(-cell.y, cell.x, 0f),
			_ => new Vector3(cell.x, cell.y, 0f),
		};
	}
}
