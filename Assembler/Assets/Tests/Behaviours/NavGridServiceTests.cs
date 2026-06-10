using System.Linq;
using Assembler.Behaviours.AI;
using Assembler.Parsing.Info;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	public class NavGridServiceTests
	{
		// Empty obstacle tag => CreateGrid skips rasterization, so the grid is all-walkable and the test is
		// independent of whatever GameEntities happen to exist in the scene.
		private static NavGridService Service(NavPlane plane) =>
			new(NavGridSettings.Default with { Plane = plane, ObstacleTag = "" });

		[Test]
		public void FlowDirectionXyIsInXyPlane()
		{
			var dir = Service(NavPlane.XY).FlowDirection(new Vector3(0, 0, 0), new Vector3(0, 5, 0));

			Assert.AreEqual(0f, dir.z, 1e-4f, "off-plane (Z) component is zero in XY");
			Assert.Greater(dir.y, 0f, "should step toward the +Y goal");
		}

		[Test]
		public void FlowDirectionXzIsInXzPlane()
		{
			var dir = Service(NavPlane.XZ).FlowDirection(new Vector3(0, 0, 0), new Vector3(0, 0, 5));

			Assert.AreEqual(0f, dir.y, 1e-4f, "off-plane (Y) component is zero in XZ");
			Assert.Greater(dir.z, 0f, "should step toward the +Z goal");
		}

		[Test]
		public void PathXzKeepsGoalHeightAndArrivesExactly()
		{
			var to = new Vector3(5, 2, 3);
			var path = Service(NavPlane.XZ).Path(new Vector3(0, 2, 0), to);

			Assert.IsTrue(path.All(p => Mathf.Abs(p.y - to.y) < 1e-4f), "every waypoint keeps the goal's Y (off-plane)");
			Assert.AreEqual(to, path[^1], "final waypoint is the exact goal");
		}

		[Test]
		public void PathXyKeepsGoalDepthAndArrivesExactly()
		{
			var to = new Vector3(5, 3, 9);
			var path = Service(NavPlane.XY).Path(new Vector3(0, 0, 9), to);

			Assert.IsTrue(path.All(p => Mathf.Abs(p.z - to.z) < 1e-4f), "every waypoint keeps the goal's Z (off-plane)");
			Assert.AreEqual(to, path[^1], "final waypoint is the exact goal");
		}
	}
}
