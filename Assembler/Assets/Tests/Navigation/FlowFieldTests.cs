using Assembler.Navigation;
using NUnit.Framework;

namespace Tests.Navigation
{
	public class FlowFieldTests
	{
		private static NavGrid Open(int width, int height) =>
			NavGrid.Create(0f, 0f, width, height, 1f);

		[Test]
		public void DirectionPointsTowardGoal()
		{
			var grid = Open(6, 1);
			var field = FlowField.Build(grid, new GridCoord(5, 0));

			var (x, _) = field.Direction(new GridCoord(0, 0));
			Assert.Greater(x, 0f, "should step toward the goal in +x");
		}

		[Test]
		public void GoalCellHasNoDirection()
		{
			var grid = Open(6, 1);
			var field = FlowField.Build(grid, new GridCoord(5, 0));

			var (x, y) = field.Direction(new GridCoord(5, 0));
			Assert.AreEqual(0f, x);
			Assert.AreEqual(0f, y);
		}

		[Test]
		public void EveryReachableCellHasPath()
		{
			var grid = Open(4, 4);
			var field = FlowField.Build(grid, new GridCoord(0, 0));

			Assert.IsTrue(field.HasPath(new GridCoord(3, 3)));
		}

		[Test]
		public void WalledOffCellHasNoPath()
		{
			var grid = Open(5, 1);
			// Wall cell 2 isolates cells 3 and 4 from the goal at 0 (1-tall grid, no detour).
			grid.SetWalkable(new GridCoord(2, 0), false);

			var field = FlowField.Build(grid, new GridCoord(0, 0));

			Assert.IsFalse(field.HasPath(new GridCoord(4, 0)));
			var (x, y) = field.Direction(new GridCoord(4, 0));
			Assert.AreEqual(0f, x);
			Assert.AreEqual(0f, y);
		}

		[Test]
		public void IsDeterministic()
		{
			var grid = Open(6, 6);
			var a = FlowField.Build(grid, new GridCoord(0, 0));
			var b = FlowField.Build(grid, new GridCoord(0, 0));

			for (var y = 0; y < 6; y++)
			{
				for (var x = 0; x < 6; x++)
				{
					Assert.AreEqual(a.Direction(new GridCoord(x, y)), b.Direction(new GridCoord(x, y)));
				}
			}
		}
	}
}
