using System.Linq;
using Assembler.Navigation;
using NUnit.Framework;

namespace Tests.Navigation
{
	public class AStarTests
	{
		private static NavGrid Open(int width, int height) =>
			NavGrid.Create(0f, 0f, width, height, 1f);

		private static void WallColumn(NavGrid grid, int x, int fromY, int toY)
		{
			for (var y = fromY; y <= toY; y++)
			{
				grid.SetWalkable(new GridCoord(x, y), false);
			}
		}

		[Test]
		public void FindsPathInOpenGrid()
		{
			var grid = Open(6, 6);
			var path = AStar.FindPath(grid, new GridCoord(0, 0), new GridCoord(5, 0));

			Assert.Greater(path.Count, 0);
			Assert.AreEqual(new GridCoord(0, 0), path[0]);
			Assert.AreEqual(new GridCoord(5, 0), path[^1]);
		}

		[Test]
		public void DeclinesWhenGoalWalledOff()
		{
			var grid = Open(5, 5);
			// A full-height wall down column 2 splits the grid in two.
			WallColumn(grid, 2, 0, 4);

			var path = AStar.FindPath(grid, new GridCoord(0, 2), new GridCoord(4, 2));

			Assert.AreEqual(0, path.Count);
		}

		[Test]
		public void RoutesAroundPartialWall()
		{
			var grid = Open(5, 5);
			// Wall column 2 for rows 0..3, leaving row 4 open — the path must detour through the gap.
			WallColumn(grid, 2, 0, 3);

			var path = AStar.FindPath(grid, new GridCoord(0, 0), new GridCoord(4, 0));

			Assert.Greater(path.Count, 0);
			Assert.IsTrue(path.Any(c => c.Y == 4), "path should detour through the open row");
			Assert.IsFalse(path.Any(c => c.X == 2 && c.Y <= 3), "path must not cross the wall");
		}

		[Test]
		public void UnwalkableEndpointsYieldNoPath()
		{
			var grid = Open(4, 4);
			grid.SetWalkable(new GridCoord(3, 3), false);

			Assert.AreEqual(0, AStar.FindPath(grid, new GridCoord(0, 0), new GridCoord(3, 3)).Count);
		}

		[Test]
		public void IsDeterministic()
		{
			var grid = Open(8, 8);
			WallColumn(grid, 4, 0, 6);

			var a = AStar.FindPath(grid, new GridCoord(0, 0), new GridCoord(7, 7));
			var b = AStar.FindPath(grid, new GridCoord(0, 0), new GridCoord(7, 7));

			CollectionAssert.AreEqual(a.ToArray(), b.ToArray());
		}
	}
}
