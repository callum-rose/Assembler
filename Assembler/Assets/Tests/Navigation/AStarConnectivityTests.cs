using System;
using Assembler.Navigation;
using NUnit.Framework;

namespace Tests.Navigation
{
	public class AStarConnectivityTests
	{
		private static NavGrid Open(int width, int height) => NavGrid.Create(0f, 0f, width, height, 1f);

		private static bool IsDiagonalStep(GridCoord a, GridCoord b) =>
			Math.Abs(a.X - b.X) == 1 && Math.Abs(a.Y - b.Y) == 1;

		[Test]
		public void EightConnectedTakesDiagonalShortcut()
		{
			var grid = Open(6, 6);
			var path = AStar.FindPath(grid, new GridCoord(0, 0), new GridCoord(3, 3), allowDiagonal: true);

			// Pure diagonal: (0,0),(1,1),(2,2),(3,3) — four cells, and at least one diagonal step.
			Assert.AreEqual(4, path.Count);
			var diagonal = false;
			for (var i = 1; i < path.Count; i++)
			{
				diagonal |= IsDiagonalStep(path[i - 1], path[i]);
			}

			Assert.IsTrue(diagonal, "eight-connected path should contain a diagonal step");
		}

		[Test]
		public void FourConnectedNeverStepsDiagonally()
		{
			var grid = Open(6, 6);
			var path = AStar.FindPath(grid, new GridCoord(0, 0), new GridCoord(3, 3), allowDiagonal: false);

			// Manhattan distance 6 -> 7 cells, and every step changes exactly one axis by one.
			Assert.AreEqual(7, path.Count);
			for (var i = 1; i < path.Count; i++)
			{
				var (a, b) = (path[i - 1], path[i]);
				var manhattan = Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
				Assert.AreEqual(1, manhattan, $"step {a} -> {b} is not a single orthogonal move");
			}
		}

		[Test]
		public void DefaultsToEightConnected()
		{
			var grid = Open(6, 6);
			var path = AStar.FindPath(grid, new GridCoord(0, 0), new GridCoord(3, 3));
			Assert.AreEqual(4, path.Count, "the default overload should remain eight-connected");
		}
	}
}
