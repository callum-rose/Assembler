using System.Collections.Generic;
using Assembler.Resolving;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Resolving
{
	public class EntityTransformRegistryTests
	{
		private readonly List<GameObject> _objects = new();

		[TearDown]
		public void TearDown()
		{
			foreach (var go in _objects)
			{
				if (go)
				{
					Object.DestroyImmediate(go);
				}
			}

			_objects.Clear();
		}

		private Transform Make(string name)
		{
			var go = new GameObject(name);
			_objects.Add(go);
			return go.transform;
		}

		[Test]
		public void GetReturnsRegisteredTransform()
		{
			var registry = new EntityTransformRegistry();
			var transform = Make("e");
			registry.Register("e", transform);

			Assert.AreSame(transform, registry.Get("e"));
		}

		[Test]
		public void UnregisterRemovesEntity()
		{
			var registry = new EntityTransformRegistry();
			registry.Register("e", Make("e"));

			registry.Unregister("e");

			Assert.Throws<System.InvalidOperationException>(() => registry.Get("e"));
		}

		[Test]
		public void UnregisterUnknownIdIsNoOp()
		{
			var registry = new EntityTransformRegistry();

			Assert.DoesNotThrow(() => registry.Unregister("missing"));
		}

		[Test]
		public void IdCanBeReRegisteredAfterUnregister()
		{
			var registry = new EntityTransformRegistry();
			registry.Register("e", Make("first"));
			registry.Unregister("e");

			var replacement = Make("second");

			Assert.DoesNotThrow(() => registry.Register("e", replacement));
			Assert.AreSame(replacement, registry.Get("e"));
		}

		[Test]
		public void GetTreatsDestroyedTransformAsAbsent()
		{
			var registry = new EntityTransformRegistry();
			var go = new GameObject("e");
			registry.Register("e", go.transform);

			// Simulate the window between Unity tearing the transform down and OnDestroy deregistering it.
			Object.DestroyImmediate(go);

			Assert.Throws<System.InvalidOperationException>(() => registry.Get("e"));
		}
	}
}
