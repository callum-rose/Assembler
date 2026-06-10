using System.Collections.Generic;
using System.Linq;
using Assembler.Resolving;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Resolving
{
	public class EntityQueryServiceTests
	{
		private readonly List<GameObject> _objects = new();

		[TearDown]
		public void TearDown()
		{
			foreach (var go in _objects)
			{
				Object.DestroyImmediate(go);
			}

			_objects.Clear();
		}

		private Transform At(string name, Vector3 position)
		{
			var go = new GameObject(name) { transform = { position = position } };
			_objects.Add(go);
			return go.transform;
		}

		[Test]
		public void NearestPicksClosestWithTag()
		{
			var service = new EntityQueryService();
			service.Register("far", At("far", new Vector3(10, 0, 0)), new[] { "enemy" });
			service.Register("near", At("near", new Vector3(2, 0, 0)), new[] { "enemy" });
			service.Register("other", At("other", new Vector3(1, 0, 0)), new[] { "ally" });

			Assert.IsTrue(service.TryNearest(Vector3.zero, "enemy", 100f, out var id));
			Assert.AreEqual("near", id);
		}

		[Test]
		public void NearestRespectsMaxRange()
		{
			var service = new EntityQueryService();
			service.Register("e", At("e", new Vector3(5, 0, 0)), new[] { "enemy" });

			Assert.IsFalse(service.TryNearest(Vector3.zero, "enemy", 4f, out _));
			Assert.IsTrue(service.TryNearest(Vector3.zero, "enemy", 6f, out var id));
			Assert.AreEqual("e", id);
		}

		[Test]
		public void NearestBreaksTiesByLowestId()
		{
			var service = new EntityQueryService();
			// Registered out of id order; both equidistant. The id-sorted bucket must make "a" win.
			service.Register("b", At("b", new Vector3(3, 0, 0)), new[] { "enemy" });
			service.Register("a", At("a", new Vector3(3, 0, 0)), new[] { "enemy" });

			Assert.IsTrue(service.TryNearest(Vector3.zero, "enemy", 100f, out var id));
			Assert.AreEqual("a", id);
		}

		[Test]
		public void WithinRadiusReturnsAllInRangeIdSorted()
		{
			var service = new EntityQueryService();
			service.Register("c", At("c", new Vector3(1, 0, 0)), new[] { "enemy" });
			service.Register("a", At("a", new Vector3(2, 0, 0)), new[] { "enemy" });
			service.Register("b", At("b", new Vector3(50, 0, 0)), new[] { "enemy" });

			CollectionAssert.AreEqual(new[] { "a", "c" }, service.WithinRadius(Vector3.zero, "enemy", 5f).ToArray());
		}

		[Test]
		public void WithinConeFiltersByAngle()
		{
			var service = new EntityQueryService();
			service.Register("ahead", At("ahead", new Vector3(3, 0, 0)), new[] { "enemy" });
			service.Register("behind", At("behind", new Vector3(-3, 0, 0)), new[] { "enemy" });

			var inCone = service.WithinCone(Vector3.zero, Vector3.right, "enemy", 10f, 45f);

			CollectionAssert.AreEqual(new[] { "ahead" }, inCone.ToArray());
		}

		[Test]
		public void DestroyedEntitiesAreSkipped()
		{
			var service = new EntityQueryService();
			var transform = At("gone", new Vector3(1, 0, 0));
			service.Register("gone", transform, new[] { "enemy" });

			Object.DestroyImmediate(transform.gameObject);
			_objects.Clear();

			Assert.IsFalse(service.TryNearest(Vector3.zero, "enemy", 100f, out _));
		}

		[Test]
		public void UnregisterRemovesEntity()
		{
			var service = new EntityQueryService();
			service.Register("e", At("e", new Vector3(1, 0, 0)), new[] { "enemy" });

			Assert.IsTrue(service.TryNearest(Vector3.zero, "enemy", 100f, out _));

			service.Unregister("e");

			Assert.IsFalse(service.TryNearest(Vector3.zero, "enemy", 100f, out _));
			Assert.IsFalse(service.TryGetPosition("e", out _));
		}
	}
}
