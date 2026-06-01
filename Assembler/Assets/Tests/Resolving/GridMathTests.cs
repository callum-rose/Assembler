using System;
using System.Collections.Generic;
using Assembler.Compiler.Compiler;
using Assembler.Libraries;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Resolving
{
	public class GridMathTests
	{
		private const float Tol = 1e-4f;

		private static void AssertCell(Vector3 actual, float x, float y)
		{
			Assert.That(actual.x, Is.EqualTo(x).Within(Tol), "x");
			Assert.That(actual.y, Is.EqualTo(y).Within(Tol), "y");
			Assert.That(actual.z, Is.EqualTo(0f).Within(Tol), "z");
		}

		private static Vector3 Cell(float col, float row) => new(col, row, 0f);

		// ---- cell <-> world ---------------------------------------------------

		[Test]
		public void CellToWorldAppliesOrigin()
		{
			AssertCell(GridMath.CellToWorld(Cell(0, 0), -4.5f, -9.5f), -4.5f, -9.5f);
			AssertCell(GridMath.CellToWorld(Cell(4, 18), -4.5f, -9.5f), -0.5f, 8.5f);
		}

		[Test]
		public void CellToWorldScalesByCellSize()
		{
			AssertCell(GridMath.CellToWorld(Cell(2, 3), -4.5f, -9.5f, 2f), -0.5f, -3.5f);
		}

		[Test]
		public void WorldToCellRoundTrips()
		{
			foreach (var cell in new[] { Cell(0, 0), Cell(9, 19), Cell(4, 18) })
			{
				var world = GridMath.CellToWorld(cell, -4.5f, -9.5f);
				AssertCell(GridMath.WorldToCell(world, -4.5f, -9.5f), cell.x, cell.y);
			}
		}

		// ---- bounds -----------------------------------------------------------

		[Test]
		public void InBoundsRespectsWidthAndHeight()
		{
			Assert.That(GridMath.InBounds(0, 0, 10, 20), Is.True);
			Assert.That(GridMath.InBounds(9, 19, 10, 20), Is.True);
			Assert.That(GridMath.InBounds(10, 0, 10, 20), Is.False);
			Assert.That(GridMath.InBounds(0, 20, 10, 20), Is.False);
			Assert.That(GridMath.InBounds(-1, 0, 10, 20), Is.False);
			Assert.That(GridMath.InBounds(0, -1, 10, 20), Is.False);
		}

		[Test]
		public void InBoundsOpenTopHasNoCeiling()
		{
			Assert.That(GridMath.InBoundsOpenTop(0, 0, 10), Is.True);
			Assert.That(GridMath.InBoundsOpenTop(9, 0, 10), Is.True);
			// Above the well is allowed (no row ceiling).
			Assert.That(GridMath.InBoundsOpenTop(5, 999, 10), Is.True);
			Assert.That(GridMath.InBoundsOpenTop(-1, 0, 10), Is.False);
			Assert.That(GridMath.InBoundsOpenTop(10, 0, 10), Is.False);
			Assert.That(GridMath.InBoundsOpenTop(0, -1, 10), Is.False);
		}

		// ---- occupancy --------------------------------------------------------

		private static List<Vector3> Occupied(params (float col, float row)[] cells)
		{
			var list = new List<Vector3>();
			foreach (var (col, row) in cells)
			{
				list.Add(Cell(col, row));
			}
			return list;
		}

		[Test]
		public void IsOccupiedDetectsPresentAndAbsentCells()
		{
			var occupied = Occupied((1, 1), (2, 1));
			Assert.That(GridMath.IsOccupied(occupied, 1, 1), Is.True);
			Assert.That(GridMath.IsOccupied(occupied, 2, 1), Is.True);
			Assert.That(GridMath.IsOccupied(occupied, 0, 1), Is.False);
			Assert.That(GridMath.IsOccupied(occupied, 1, 2), Is.False);
		}

		[Test]
		public void CellsInRowAndRowFull()
		{
			var occupied = Occupied((0, 0), (1, 0), (2, 0), (0, 1));
			Assert.That(GridMath.CellsInRow(occupied, 0), Is.EqualTo(3));
			Assert.That(GridMath.CellsInRow(occupied, 1), Is.EqualTo(1));
			Assert.That(GridMath.RowFull(occupied, 0, 3), Is.True);
			Assert.That(GridMath.RowFull(occupied, 0, 4), Is.False);
			Assert.That(GridMath.RowFull(occupied, 1, 3), Is.False);
		}

		[Test]
		public void FullRowCountCountsCompleteRows()
		{
			// rows 0 and 2 full (width 3), row 1 partial.
			var occupied = Occupied((0, 0), (1, 0), (2, 0), (0, 1), (0, 2), (1, 2), (2, 2));
			Assert.That(GridMath.FullRowCount(occupied, 3, 4), Is.EqualTo(2));
			Assert.That(GridMath.FullRowCount(Occupied(), 3, 4), Is.EqualTo(0));
		}

		[Test]
		public void FullRowsBelowOnlyCountsRowsUnderTheGivenRow()
		{
			// rows 0 and 2 full (width 3).
			var occupied = Occupied((0, 0), (1, 0), (2, 0), (0, 2), (1, 2), (2, 2));
			Assert.That(GridMath.FullRowsBelow(occupied, 5, 3, 4), Is.EqualTo(2));
			Assert.That(GridMath.FullRowsBelow(occupied, 1, 3, 4), Is.EqualTo(1));
			Assert.That(GridMath.FullRowsBelow(occupied, 0, 3, 4), Is.EqualTo(0));
		}

		// ---- neighbours + rotation -------------------------------------------

		[Test]
		public void NeighbourCellOffsets()
		{
			AssertCell(GridMath.NeighbourCell(Cell(1, 1), 1, -1), 2, 0);
			AssertCell(GridMath.NeighbourCell(Cell(5, 5), -2, 3), 3, 8);
		}

		[Test]
		public void RotateCellCWQuarterTurns()
		{
			var c = Cell(1, 0);
			AssertCell(GridMath.RotateCellCW(c, 0), 1, 0);
			AssertCell(GridMath.RotateCellCW(c, 1), 0, -1);
			AssertCell(GridMath.RotateCellCW(c, 2), -1, 0);
			AssertCell(GridMath.RotateCellCW(c, 3), 0, 1);
			// Four quarter-turns is the identity.
			AssertCell(GridMath.RotateCellCW(c, 4), 1, 0);
		}

		// ---- compiler integration (real risk surface) ------------------------
		// Proves GridMath is globally registered by CompiledExpressionsRegistry,
		// callable by bare name from a descriptor expression, with int->float
		// argument coercion.

		private static CompiledExpressionsRegistry NewRegistry() =>
			new(BuiltInTypeRegistry.Default, new ExpressionMethodCompiler());

		private static ExpressionInfo Expr(string id, string returnType, string body,
			IReadOnlyList<(string type, string name)>? args = null) =>
			new(id, args ?? Array.Empty<(string, string)>(), returnType,
				Array.Empty<string>(), Array.Empty<string>(), body);

		[Test]
		public void ExpressionCanCallCellToWorldByBareName()
		{
			var registry = NewRegistry();
			registry.CompileAndRegisterAll(new[]
			{
				Expr("to world", "vector", "return CellToWorld(cell, -4.5f, -9.5f);",
					new[] { ("vector", "cell") }),
			});

			var func = (Func<Vector3, Vector3>)registry.GetCompiled("to world").@delegate;

			AssertCell(func(Cell(0, 0)), -4.5f, -9.5f);
			AssertCell(func(Cell(4, 18)), -0.5f, 8.5f);
		}

		[Test]
		public void ExpressionCoercesIntArgsToFloatGridHelpers()
		{
			var registry = NewRegistry();
			registry.CompileAndRegisterAll(new[]
			{
				// col/row are int args, 10 is an int literal — all must coerce to
				// the float parameters of InBoundsOpenTop.
				Expr("in well", "bool", "return InBoundsOpenTop(col, row, 10);",
					new[] { ("int", "col"), ("int", "row") }),
			});

			var func = (Func<int, int, bool>)registry.GetCompiled("in well").@delegate;

			Assert.That(func(5, 5), Is.True);
			Assert.That(func(9, 0), Is.True);
			Assert.That(func(10, 0), Is.False);
			Assert.That(func(-1, 0), Is.False);
			Assert.That(func(0, -1), Is.False);
		}
	}
}
