using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Behaviours.AI;
using Assembler.Parsing.Info;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	public class NavGridServiceTests
	{
		// A tag unique to these tests so the service rasterizes only the obstacles each test creates, never
		// stray "obstacle"-tagged entities another test might leave in the edit-mode scene.
		private const string ObstacleTag = "nav-obstacle-test";

		private readonly List<GameObject> _created = new();

		[TearDown]
		public void TearDown()
		{
			foreach (var go in _created)
			{
				if (go)
				{
					Object.DestroyImmediate(go);
				}
			}

			_created.Clear();
		}

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

		[Test]
		public void SphereCornerCellsOpenXy()
		{
			AddObstacle<SphereCollider>(Vector3.zero).radius = 2f;
			var service = ObstacleService(NavPlane.XY);

			Assert.IsFalse(service.IsWalkable(new Vector3(0f, 0f, 0f)), "the sphere centre is blocked");
			Assert.IsTrue(service.IsWalkable(new Vector3(2.5f, 2.5f, 0f)),
				"an AABB corner the sphere does not reach stays open (the cell the old bounds rasterizer wrongly blocked)");
		}

		[Test]
		public void SphereCornerCellsOpenXz()
		{
			AddObstacle<SphereCollider>(Vector3.zero).radius = 2f;
			var service = ObstacleService(NavPlane.XZ);

			Assert.IsFalse(service.IsWalkable(new Vector3(0f, 0f, 0f)), "the sphere centre is blocked");
			Assert.IsTrue(service.IsWalkable(new Vector3(2.5f, 0f, 2.5f)),
				"the open AABB corner is honoured on the XZ plane too");
		}

		[Test]
		public void CapsuleDoesNotOverBlockEnds()
		{
			var capsule = AddObstacle<CapsuleCollider>(Vector3.zero);
			capsule.radius = 1f;
			capsule.height = 4f;
			var service = ObstacleService(NavPlane.XY);

			Assert.IsFalse(service.IsWalkable(new Vector3(0f, 0f, 0f)), "the capsule body is blocked");
			Assert.IsTrue(service.IsWalkable(new Vector3(1.5f, 2.5f, 0f)),
				"the AABB corner past the rounded cap stays open");
		}

		[Test]
		public void AxisAlignedBoxBlocksItsCellAndNotNeighbours()
		{
			// Sized to sit wholly within one cell so the (inclusive) bounds rasterizer blocks exactly cell (50,50).
			AddObstacle<BoxCollider>(new Vector3(0.5f, 0.5f, 0f)).size = new Vector3(0.8f, 0.8f, 1f);
			var service = ObstacleService(NavPlane.XY);

			Assert.IsFalse(service.IsWalkable(new Vector3(0.5f, 0.5f, 0f)), "the covered cell is blocked");
			Assert.IsTrue(service.IsWalkable(new Vector3(1.5f, 0.5f, 0f)), "the right neighbour stays open");
			Assert.IsTrue(service.IsWalkable(new Vector3(0.5f, 1.5f, 0f)), "the upper neighbour stays open");
		}

		[Test]
		public void OffGridColliderDoesNotSmearBoundary()
		{
			// Entirely outside the default -50..50 grid: it must not paint a phantom wall on the boundary.
			AddObstacle<SphereCollider>(new Vector3(100f, 100f, 0f)).radius = 2f;
			var service = ObstacleService(NavPlane.XY);

			Assert.IsTrue(service.IsWalkable(new Vector3(49.5f, 49.5f, 0f)), "the near boundary cell stays walkable");
		}

		[Test]
		public void AgentRadiusInflatesClearanceAroundObstacle()
		{
			AddObstacle<SphereCollider>(Vector3.zero).radius = 0.4f;

			Assert.IsTrue(ObstacleService(NavPlane.XY).IsWalkable(new Vector3(2.5f, 0.5f, 0f)),
				"without inflation a cell two over is open");

			var inflated = ObstacleService(NavPlane.XY, agentRadius: 2f);
			Assert.IsFalse(inflated.IsWalkable(new Vector3(2.5f, 0.5f, 0f)), "clearance reaches two cells out");
			Assert.IsTrue(inflated.IsWalkable(new Vector3(5.5f, 0.5f, 0f)), "a cell beyond the radius stays open");
		}

		// A grid service whose obstacles are exactly the test-created entities tagged ObstacleTag.
		private static NavGridService ObstacleService(NavPlane plane, float agentRadius = 0f) =>
			new(NavGridSettings.Default with { Plane = plane, ObstacleTag = ObstacleTag, AgentRadius = agentRadius });

		// Creates an active obstacle-tagged entity at a world position with a collider of the requested shape,
		// tracked for teardown. The caller configures the collider's dimensions on the returned component.
		private T AddObstacle<T>(Vector3 position) where T : Collider
		{
			var go = new GameObject("nav-obstacle");
			_created.Add(go);
			go.transform.position = position;
			go.AddComponent<GameEntity>().Tags = new[] { ObstacleTag };
			return go.AddComponent<T>();
		}
	}
}
