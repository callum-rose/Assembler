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
			var dir = Service(NavPlane.XY).FlowDirection(new Vector3(0, 0, 0), new Vector3(0, 5, 0), 0f);

			Assert.AreEqual(0f, dir.z, 1e-4f, "off-plane (Z) component is zero in XY");
			Assert.Greater(dir.y, 0f, "should step toward the +Y goal");
		}

		[Test]
		public void FlowDirectionXzIsInXzPlane()
		{
			var dir = Service(NavPlane.XZ).FlowDirection(new Vector3(0, 0, 0), new Vector3(0, 0, 5), 0f);

			Assert.AreEqual(0f, dir.y, 1e-4f, "off-plane (Y) component is zero in XZ");
			Assert.Greater(dir.z, 0f, "should step toward the +Z goal");
		}

		[Test]
		public void PathXzKeepsGoalHeightAndArrivesExactly()
		{
			var to = new Vector3(5, 2, 3);
			var path = Service(NavPlane.XZ).Path(new Vector3(0, 2, 0), to, 0f);

			Assert.IsTrue(path.All(p => Mathf.Abs(p.y - to.y) < 1e-4f), "every waypoint keeps the goal's Y (off-plane)");
			Assert.AreEqual(to, path[^1], "final waypoint is the exact goal");
		}

		[Test]
		public void PathXyKeepsGoalDepthAndArrivesExactly()
		{
			var to = new Vector3(5, 3, 9);
			var path = Service(NavPlane.XY).Path(new Vector3(0, 0, 9), to, 0f);

			Assert.IsTrue(path.All(p => Mathf.Abs(p.z - to.z) < 1e-4f), "every waypoint keeps the goal's Z (off-plane)");
			Assert.AreEqual(to, path[^1], "final waypoint is the exact goal");
		}

		[Test]
		public void SphereCornerCellsOpenXy()
		{
			AddObstacle<SphereCollider>(Vector3.zero).radius = 2f;
			var service = ObstacleService(NavPlane.XY);

			Assert.IsFalse(service.IsWalkable(new Vector3(0f, 0f, 0f), 0f), "the sphere centre is blocked");
			Assert.IsTrue(service.IsWalkable(new Vector3(2.5f, 2.5f, 0f), 0f),
				"an AABB corner the sphere does not reach stays open (the cell the old bounds rasterizer wrongly blocked)");
		}

		[Test]
		public void SphereCornerCellsOpenXz()
		{
			AddObstacle<SphereCollider>(Vector3.zero).radius = 2f;
			var service = ObstacleService(NavPlane.XZ);

			Assert.IsFalse(service.IsWalkable(new Vector3(0f, 0f, 0f), 0f), "the sphere centre is blocked");
			Assert.IsTrue(service.IsWalkable(new Vector3(2.5f, 0f, 2.5f), 0f),
				"the open AABB corner is honoured on the XZ plane too");
		}

		[Test]
		public void CapsuleDoesNotOverBlockEnds()
		{
			var capsule = AddObstacle<CapsuleCollider>(Vector3.zero);
			capsule.radius = 1f;
			capsule.height = 4f;
			var service = ObstacleService(NavPlane.XY);

			Assert.IsFalse(service.IsWalkable(new Vector3(0f, 0f, 0f), 0f), "the capsule body is blocked");
			Assert.IsTrue(service.IsWalkable(new Vector3(1.5f, 2.5f, 0f), 0f),
				"the AABB corner past the rounded cap stays open");
		}

		[Test]
		public void AxisAlignedBoxBlocksItsCellAndNotNeighbours()
		{
			// Sized to sit wholly within one cell so the (inclusive) bounds rasterizer blocks exactly cell (50,50).
			AddObstacle<BoxCollider>(new Vector3(0.5f, 0.5f, 0f)).size = new Vector3(0.8f, 0.8f, 1f);
			var service = ObstacleService(NavPlane.XY);

			Assert.IsFalse(service.IsWalkable(new Vector3(0.5f, 0.5f, 0f), 0f), "the covered cell is blocked");
			Assert.IsTrue(service.IsWalkable(new Vector3(1.5f, 0.5f, 0f), 0f), "the right neighbour stays open");
			Assert.IsTrue(service.IsWalkable(new Vector3(0.5f, 1.5f, 0f), 0f), "the upper neighbour stays open");
		}

		[Test]
		public void OffGridColliderDoesNotSmearBoundary()
		{
			// Entirely outside the default -50..50 grid: it must not paint a phantom wall on the boundary.
			AddObstacle<SphereCollider>(new Vector3(100f, 100f, 0f)).radius = 2f;
			var service = ObstacleService(NavPlane.XY);

			Assert.IsTrue(service.IsWalkable(new Vector3(49.5f, 49.5f, 0f), 0f), "the near boundary cell stays walkable");
		}

		[Test]
		public void DifferentAgentRadiiClearDifferentlyOnOneService()
		{
			AddObstacle<SphereCollider>(Vector3.zero).radius = 0.4f;
			var service = ObstacleService(NavPlane.XY);
			var nearObstacle = new Vector3(2.5f, 0.5f, 0f);

			// One shared service, queried with two radii: a point clear for a small agent is blocked for a large
			// one, so the two take different paths — and a cell beyond even the large radius stays open.
			Assert.IsTrue(service.IsWalkable(nearObstacle, 0f), "a small agent (radius 0) may pass close by");
			Assert.IsFalse(service.IsWalkable(nearObstacle, 2f), "a large agent (radius 2) is kept clear");
			Assert.IsTrue(service.IsWalkable(new Vector3(5.5f, 0.5f, 0f), 2f), "a cell beyond the radius stays open");

			// Querying the large radius must not mutate the base grid the small radius reads.
			Assert.IsTrue(service.IsWalkable(nearObstacle, 0f),
				"the radius-0 grid is unchanged after a larger radius is queried");
		}

		[Test]
		public void ServiceExposesTheConfiguredDefaultAgentRadius()
		{
			// The navigate / grid mover behaviours read this as the fallback for an unset per-agent AgentRadius
			// (via AgentRadius.ValueOr(ctx, Nav.DefaultAgentRadius)); the negative-sentinel approach is gone.
			Assert.AreEqual(2f, ObstacleService(NavPlane.XY, defaultAgentRadius: 2f).DefaultAgentRadius, 1e-4f);
			Assert.AreEqual(0f, ObstacleService(NavPlane.XY).DefaultAgentRadius, 1e-4f);
		}

		[Test]
		public void DynamicObstacleBlocksAndReopensItsCell()
		{
			// The nav obstacle behaviour drives this: destructible/moving terrain toggles walkability at runtime,
			// unlike the baked static grid. A cell open on the empty grid blocks while an obstacle sits on it and
			// reopens the moment the obstacle is toggled off — no rebuild, no waiting for collider teardown.
			var service = Service(NavPlane.XY);
			var at = new Vector3(0.5f, 0.5f, 0f);
			var footprint = new Bounds(at, new Vector3(0.8f, 0.8f, 1f));

			Assert.IsTrue(service.IsWalkable(at, 0f), "the empty grid starts walkable");

			service.SetObstacle(new object(), footprint, blocked: true);
			Assert.IsFalse(service.IsWalkable(at, 0f), "the cell blocks while the obstacle occupies it");
		}

		[Test]
		public void TogglingADynamicObstacleOffReopensItsCell()
		{
			var service = Service(NavPlane.XY);
			var at = new Vector3(0.5f, 0.5f, 0f);
			var footprint = new Bounds(at, new Vector3(0.8f, 0.8f, 1f));
			var handle = new object();

			service.SetObstacle(handle, footprint, blocked: true);
			service.SetObstacle(handle, footprint, blocked: false);
			Assert.IsTrue(service.IsWalkable(at, 0f), "toggling Blocked false reopens the cell");

			service.SetObstacle(handle, footprint, blocked: true);
			service.RemoveObstacle(handle);
			Assert.IsTrue(service.IsWalkable(at, 0f), "the OnDestroy leak-guard (RemoveObstacle) reopens the cell too");
		}

		[Test]
		public void OverlappingDynamicObstaclesAreRefcounted()
		{
			// Two obstacles on the same cell: removing one must not reopen it while the other is still there —
			// otherwise a destroyed block would wrongly clear a cell a second block still occupies.
			var service = Service(NavPlane.XY);
			var at = new Vector3(0.5f, 0.5f, 0f);
			var footprint = new Bounds(at, new Vector3(0.8f, 0.8f, 1f));
			var first = new object();
			var second = new object();

			service.SetObstacle(first, footprint, blocked: true);
			service.SetObstacle(second, footprint, blocked: true);

			service.RemoveObstacle(first);
			Assert.IsFalse(service.IsWalkable(at, 0f), "the cell stays blocked while the second obstacle remains");

			service.RemoveObstacle(second);
			Assert.IsTrue(service.IsWalkable(at, 0f), "the cell reopens once the last obstacle leaves");
		}

		[Test]
		public void DynamicObstacleClearsInflatedGridsForRadiusAgents()
		{
			// The overlay must flow through to the per-radius inflated grids (the cache the flow fields and larger
			// agents read), not just the radius-0 base — otherwise a large agent would walk through a fresh block.
			var service = Service(NavPlane.XY);
			var at = new Vector3(0.5f, 0.5f, 0f);
			var nearBlock = new Vector3(2.5f, 0.5f, 0f);

			// Query a radius first so an inflated grid is built and cached before the overlay changes.
			Assert.IsTrue(service.IsWalkable(nearBlock, 2f), "the empty grid is clear for a large agent");

			service.SetObstacle(new object(), new Bounds(at, new Vector3(0.8f, 0.8f, 1f)), blocked: true);

			Assert.IsFalse(service.IsWalkable(nearBlock, 2f), "a large agent is kept clear of the new block");
			Assert.IsTrue(service.IsWalkable(nearBlock, 0f), "a point-sized agent may still pass close by");
		}

		// A grid service whose obstacles are exactly the test-created entities tagged ObstacleTag.
		// DefaultAgentRadius is the game-wide fallback the behaviours apply when an agent's own radius is unset;
		// the service's query methods take the per-agent radius explicitly.
		private static NavGridService ObstacleService(NavPlane plane, float defaultAgentRadius = 0f) =>
			new(NavGridSettings.Default with
			{
				Plane = plane, ObstacleTag = ObstacleTag, DefaultAgentRadius = defaultAgentRadius
			});

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
