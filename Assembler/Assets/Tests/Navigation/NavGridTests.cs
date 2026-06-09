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
	}
}
