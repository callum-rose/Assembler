using Assembler.Navigation;
using NUnit.Framework;

namespace Tests.Navigation
{
	public class NavGridTests
	{
		[Test]
		public void CreateSpansBoundsAtCellSize()
		{
			var grid = NavGrid.Create(0f, 0f, 10f, 6f, 2f);
			Assert.AreEqual(5, grid.Width);
			Assert.AreEqual(3, grid.Height);
		}

		[Test]
		public void AllCellsWalkableByDefault()
		{
			var grid = NavGrid.Create(0f, 0f, 4f, 4f, 1f);
			Assert.IsTrue(grid.IsWalkable(new GridCoord(0, 0)));
			Assert.IsTrue(grid.IsWalkable(new GridCoord(3, 3)));
		}

		[Test]
		public void OutOfBoundsIsNotWalkable()
		{
			var grid = NavGrid.Create(0f, 0f, 4f, 4f, 1f);
			Assert.IsFalse(grid.InBounds(new GridCoord(-1, 0)));
			Assert.IsFalse(grid.IsWalkable(new GridCoord(4, 4)));
		}

		[Test]
		public void WorldToCellRoundTripsThroughCellCentre()
		{
			var grid = NavGrid.Create(-5f, -5f, 5f, 5f, 1f);
			var cell = new GridCoord(3, 7);
			var (x, y) = grid.CellToWorld(cell);
			Assert.AreEqual(cell, grid.WorldToCell(x, y));
		}

		[Test]
		public void WorldToCellClampsToBounds()
		{
			var grid = NavGrid.Create(0f, 0f, 4f, 4f, 1f);
			Assert.AreEqual(new GridCoord(0, 0), grid.WorldToCell(-100f, -100f));
			Assert.AreEqual(new GridCoord(3, 3), grid.WorldToCell(100f, 100f));
		}

		[Test]
		public void BlockWorldRectBlocksOverlappingCells()
		{
			var grid = NavGrid.Create(0f, 0f, 4f, 4f, 1f);
			grid.BlockWorldRect(1f, 1f, 2.5f, 2.5f);

			Assert.IsFalse(grid.IsWalkable(new GridCoord(1, 1)));
			Assert.IsFalse(grid.IsWalkable(new GridCoord(2, 2)));
			Assert.IsTrue(grid.IsWalkable(new GridCoord(0, 0)));
			Assert.IsTrue(grid.IsWalkable(new GridCoord(3, 3)));
		}

		[Test]
		public void BlockWorldRectIgnoresFullyOffGridRect()
		{
			var grid = NavGrid.Create(0f, 0f, 4f, 4f, 1f);
			// Entirely to the left of the grid: must NOT smear into a phantom wall on column 0 (the failure
			// mode of clamping WorldToCell without an overlap guard).
			grid.BlockWorldRect(-10f, 1f, -6f, 3f);

			for (var y = 0; y < 4; y++)
			{
				Assert.IsTrue(grid.IsWalkable(new GridCoord(0, y)), $"column 0 row {y} should stay walkable");
			}
		}

		[Test]
		public void BlockWorldRectTreatsMaxFaceAsHalfOpen()
		{
			var grid = NavGrid.Create(0f, 0f, 4f, 4f, 1f);
			// A wall covering exactly cell (1,1): its far faces sit on the 2.0 boundaries. It must block only
			// (1,1), not bleed into the open cells past the flush faces.
			grid.BlockWorldRect(1f, 1f, 2f, 2f);

			Assert.IsFalse(grid.IsWalkable(new GridCoord(1, 1)));
			Assert.IsTrue(grid.IsWalkable(new GridCoord(2, 1)), "the cell past the flush right face must stay open");
			Assert.IsTrue(grid.IsWalkable(new GridCoord(1, 2)), "the cell past the flush top face must stay open");
			Assert.IsTrue(grid.IsWalkable(new GridCoord(2, 2)));
		}

		[Test]
		public void BlockWorldRectClampsPartiallyOverlappingRect()
		{
			var grid = NavGrid.Create(0f, 0f, 4f, 4f, 1f);
			// Straddles the left edge: the in-bounds portion (column 0) is blocked, nothing spurious beyond it.
			grid.BlockWorldRect(-2f, 0f, 0.5f, 0.5f);

			Assert.IsFalse(grid.IsWalkable(new GridCoord(0, 0)));
			Assert.IsTrue(grid.IsWalkable(new GridCoord(1, 0)));
		}

		[Test]
		public void OverlapsWorldRectMatchesGuard()
		{
			var grid = NavGrid.Create(0f, 0f, 4f, 4f, 1f);

			Assert.IsFalse(grid.OverlapsWorldRect(-10f, 1f, -6f, 3f), "fully off the left edge does not overlap");
			Assert.IsTrue(grid.OverlapsWorldRect(1f, 1f, 2f, 2f), "an interior rect overlaps");
			Assert.IsTrue(grid.OverlapsWorldRect(-2f, 0f, 0.5f, 0.5f), "a rect straddling the edge overlaps");
		}

		[Test]
		public void InflateGrowsBlockedRegionByRadius()
		{
			var grid = NavGrid.Create(0f, 0f, 5f, 5f, 1f);
			grid.SetWalkable(new GridCoord(2, 2), false);
			grid.Inflate(1f);

			// A radius of one cell stamps the orthogonal neighbours (Euclidean disk excludes the diagonals).
			Assert.IsFalse(grid.IsWalkable(new GridCoord(1, 2)), "left neighbour gets clearance");
			Assert.IsFalse(grid.IsWalkable(new GridCoord(3, 2)), "right neighbour gets clearance");
			Assert.IsFalse(grid.IsWalkable(new GridCoord(2, 1)), "lower neighbour gets clearance");
			Assert.IsTrue(grid.IsWalkable(new GridCoord(1, 1)), "diagonal is outside a one-cell disk");
			Assert.IsTrue(grid.IsWalkable(new GridCoord(0, 0)), "far cell stays walkable");
		}

		[Test]
		public void InflateWithZeroRadiusIsNoOp()
		{
			var grid = NavGrid.Create(0f, 0f, 5f, 5f, 1f);
			grid.SetWalkable(new GridCoord(2, 2), false);
			grid.Inflate(0f);

			Assert.IsFalse(grid.IsWalkable(new GridCoord(2, 2)), "the blocked cell stays blocked");
			Assert.IsTrue(grid.IsWalkable(new GridCoord(1, 2)), "no clearance is added at radius 0");
		}
	}
}
