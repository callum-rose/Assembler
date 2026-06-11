using System.Collections.Generic;
using Assembler.Behaviours;
using Assembler.Building;
using Assembler.Parsing.Info;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	/// <summary>
	/// Covers the OnDestroy-driven eviction added so spawn/destroy churn doesn't leak the registry
	/// (mirroring <c>EntityQueryService.Unregister</c>).
	/// </summary>
	public class BehaviourRegistryDeregistrationTests
	{
		private sealed class StubBehaviour : GameBehaviour
		{
		}

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

		private GameBehaviour MakeBehaviour(string entityId, params string[] entityTags)
		{
			var go = new GameObject(entityId);
			_created.Add(go);
			var entity = go.AddComponent<GameEntity>();
			entity.Id = entityId;
			entity.Tags = entityTags;
			return go.AddComponent<StubBehaviour>();
		}

		[Test]
		public void DeregisterEntityEvictsFromEveryIndex()
		{
			var registry = new BehaviourRegistry();
			var descriptor = new BehaviourDescriptor("enemy_1", "move");
			var behaviour = MakeBehaviour("enemy_1", "enemy");
			registry.Register(descriptor, behaviour, new[] { "ai" });

			registry.DeregisterEntity("enemy_1");

			Assert.IsFalse(registry.All.ContainsKey(descriptor));
			CollectionAssert.IsEmpty(registry.GetByEntityTag("enemy"));
			CollectionAssert.IsEmpty(registry.GetByEntityTagAndBehaviourId("enemy", "move"));
			CollectionAssert.IsEmpty(registry.GetByBehaviourTag("ai"));
		}

		[Test]
		public void DeregisterEntityLeavesOtherEntities()
		{
			var registry = new BehaviourRegistry();
			var keep = MakeBehaviour("enemy_2", "enemy");
			registry.Register(new BehaviourDescriptor("enemy_2", "move"), keep);
			registry.Register(new BehaviourDescriptor("enemy_1", "move"), MakeBehaviour("enemy_1", "enemy"));

			registry.DeregisterEntity("enemy_1");

			CollectionAssert.AreEqual(new[] { keep }, registry.GetByEntityTagAndBehaviourId("enemy", "move"));
		}

		[Test]
		public void DeregisterEntityUnknownIdIsNoOp()
		{
			var registry = new BehaviourRegistry();

			Assert.DoesNotThrow(() => registry.DeregisterEntity("nobody"));
		}

		[Test]
		public void DescriptorCanBeReRegisteredAfterDeregister()
		{
			var registry = new BehaviourRegistry();
			var descriptor = new BehaviourDescriptor("enemy_1", "move");
			registry.Register(descriptor, MakeBehaviour("enemy_1", "enemy"));
			registry.DeregisterEntity("enemy_1");

			var replacement = MakeBehaviour("enemy_1", "enemy");

			Assert.DoesNotThrow(() => registry.Register(descriptor, replacement));
			Assert.AreSame(replacement, registry[descriptor]);
		}
	}
}
