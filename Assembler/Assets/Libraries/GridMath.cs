using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assembler.Libraries
{
	/// <summary>
	/// First-class grid/tilemap helpers for grid-based games. Registered globally
	/// in CompiledExpressionsRegistry so every descriptor expression can call these
	/// by bare name (CellToWorld, RowFull, IsOccupied, ...). Cells are carried as
	/// Vector3(col, row, 0); grid dimensions/origin are passed explicitly so the
	/// helpers stay stateless. All numeric parameters are float so int arguments
	/// coerce automatically during overload resolution.
	/// </summary>
	public static class GridMath
	{
		/// <summary>World position of a cell, given the world coordinates of cell (0,0).</summary>
		/// <param name="cell">Cell as Vector3(col, row, 0).</param>
		/// <param name="originX">World x of cell (0,0).</param>
		/// <param name="originY">World y of cell (0,0).</param>
		/// <returns>The cell's world position.</returns>
		public static Vector3 CellToWorld(Vector3 cell, float originX, float originY) =>
			new(cell.x + originX, cell.y + originY, 0f);

		/// <summary>World position of a cell with a non-unit, square cell size.</summary>
		/// <param name="cell">Cell as Vector3(col, row, 0).</param>
		/// <param name="originX">World x of cell (0,0).</param>
		/// <param name="originY">World y of cell (0,0).</param>
		/// <param name="cellSize">Edge length of a single (square) cell in world units.</param>
		/// <returns>The cell's world position.</returns>
		public static Vector3 CellToWorld(Vector3 cell, float originX, float originY, float cellSize) =>
			new(cell.x * cellSize + originX, cell.y * cellSize + originY, 0f);

		/// <summary>Inverse of the unit-cell CellToWorld.</summary>
		/// <param name="world">A world position.</param>
		/// <param name="originX">World x of cell (0,0).</param>
		/// <param name="originY">World y of cell (0,0).</param>
		/// <returns>The cell as Vector3(col, row, 0).</returns>
		public static Vector3 WorldToCell(Vector3 world, float originX, float originY) =>
			new(world.x - originX, world.y - originY, 0f);

		/// <summary>True when col is in [0, width-1] and row is in [0, height-1].</summary>
		/// <param name="col">Column index.</param>
		/// <param name="row">Row index.</param>
		/// <param name="width">Grid width in cells.</param>
		/// <param name="height">Grid height in cells.</param>
		/// <returns>Whether the cell lies inside the grid.</returns>
		public static bool InBounds(float col, float row, float width, float height) =>
			col >= 0f && col <= width - 1f && row >= 0f && row <= height - 1f;

		/// <summary>
		/// True when col is in [0, width-1] and row &gt;= 0, with no ceiling (pieces may
		/// spawn/extend above the top of a well).
		/// </summary>
		/// <param name="col">Column index.</param>
		/// <param name="row">Row index.</param>
		/// <param name="width">Grid width in cells.</param>
		/// <returns>Whether the cell is in horizontal bounds and at or above row 0.</returns>
		public static bool InBoundsOpenTop(float col, float row, float width) => col >= 0f && col <= width - 1f && row >= 0f;

		/// <summary>True if any occupied cell sits at (col, row).</summary>
		/// <param name="occupied">The occupied cells, each as Vector3(col, row, 0).</param>
		/// <param name="col">Column index to test.</param>
		/// <param name="row">Row index to test.</param>
		/// <returns>Whether the cell is occupied.</returns>
		public static bool IsOccupied(List<Vector3> occupied, float col, float row) =>
			occupied.Any(c => Mathf.Approximately(c.x, col) && Mathf.Approximately(c.y, row));

		/// <summary>Number of occupied cells in a row.</summary>
		/// <param name="occupied">The occupied cells, each as Vector3(col, row, 0).</param>
		/// <param name="row">Row index to count.</param>
		/// <returns>The count of occupied cells in the row.</returns>
		public static int CellsInRow(List<Vector3> occupied, float row) => occupied.Count(c => Mathf.Approximately(c.y, row));

		/// <summary>True if a row is completely filled.</summary>
		/// <param name="occupied">The occupied cells, each as Vector3(col, row, 0).</param>
		/// <param name="row">Row index to test.</param>
		/// <param name="width">Grid width in cells.</param>
		/// <returns>Whether every cell in the row is occupied.</returns>
		public static bool RowFull(List<Vector3> occupied, float row, float width) =>
			CellsInRow(occupied, row) >= width;

		/// <summary>Number of completely filled rows in [0, height-1].</summary>
		/// <param name="occupied">The occupied cells, each as Vector3(col, row, 0).</param>
		/// <param name="width">Grid width in cells.</param>
		/// <param name="height">Grid height in cells.</param>
		/// <returns>The count of fully filled rows.</returns>
		public static int FullRowCount(List<Vector3> occupied, float width, float height) =>
			Enumerable.Range(0, (int)height).Count(r => RowFull(occupied, r, width));

		/// <summary>Number of completely filled rows strictly below the given row.</summary>
		/// <param name="occupied">The occupied cells, each as Vector3(col, row, 0).</param>
		/// <param name="row">Reference row; only rows with index &lt; row are counted.</param>
		/// <param name="width">Grid width in cells.</param>
		/// <param name="height">Grid height in cells.</param>
		/// <returns>The count of fully filled rows below the reference row.</returns>
		public static int FullRowsBelow(List<Vector3> occupied, float row, float width, float height) =>
			Enumerable.Range(0, (int)height).Count(r => r < row && RowFull(occupied, r, width));

		/// <summary>Cell offset from a cell by (dCol, dRow).</summary>
		/// <param name="cell">Cell as Vector3(col, row, 0).</param>
		/// <param name="dCol">Column delta.</param>
		/// <param name="dRow">Row delta.</param>
		/// <returns>The neighbouring cell.</returns>
		public static Vector3 NeighbourCell(Vector3 cell, float dCol, float dRow) =>
			new(cell.x + dCol, cell.y + dRow, 0f);

		/// <summary>
		/// Rotate a cell offset clockwise about the origin, <paramref name="times"/> quarter-turns
		/// ((x, y) -&gt; (y, -x) per turn). Four turns is the identity.
		/// </summary>
		/// <param name="cell">Cell offset as Vector3(col, row, 0).</param>
		/// <param name="times">Number of clockwise quarter-turns (any int; reduced mod 4).</param>
		/// <returns>The rotated cell offset.</returns>
		public static Vector3 RotateCellCW(Vector3 cell, int times) => ((times % 4 + 4) % 4) switch
		{
			1 => new Vector3(cell.y, -cell.x, 0f),
			2 => new Vector3(-cell.x, -cell.y, 0f),
			3 => new Vector3(-cell.y, cell.x, 0f),
			_ => new Vector3(cell.x, cell.y, 0f),
		};
	}
}
