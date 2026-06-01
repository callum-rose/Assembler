using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Building;
using Assembler.Parsing.Info;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	/// <summary>
	/// Locks in the Level 1 determinism guarantee for iteration order: <see cref="BehaviourRegistry.GetByEntityTagAndBehaviourId"/>
	/// must return results in stable registration order rather than the unordered dictionary iteration order.
	/// </summary>
	public class BehaviourRegistryOrderTests
	{
		private sealed class StubBehaviour : GameBehaviour
		{
			public override void Execute(TriggerContext ctx) { }
		}

		private readonly List<GameObject> _created = new();

		private GameBehaviour MakeBehaviour(string entityTag)
		{
			var go = new GameObject("RegistryOrderStub");
			_created.Add(go);
			var entity = go.AddComponent<GameEntity>();
			entity.Tags = new[] { entityTag };
			return go.AddComponent<StubBehaviour>();
		}

		[TearDown]
		public void TearDown()
		{
			foreach (var go in _created)
				if (go) Object.DestroyImmediate(go);
			_created.Clear();
		}

		[Test]
		public void GetByEntityTagAndBehaviourId_ReturnsResultsInRegistrationOrder()
		{
			var registry = new BehaviourRegistry();

			// Register many matching behaviours so any reordering by the underlying dictionary would surface.
			var expected = new List<GameBehaviour>();
			for (var i = 0; i < 16; i++)
			{
				var behaviour = MakeBehaviour("player");
				registry.Register(new BehaviourDescriptor($"entity_{i}", "move"), behaviour);
				expected.Add(behaviour);
			}

			var result = registry.GetByEntityTagAndBehaviourId("player", "move");

			CollectionAssert.AreEqual(expected, result);
		}

		[Test]
		public void GetByEntityTagAndBehaviourId_OrderIsIndependentOfDescriptorIdentity()
		{
			var registry = new BehaviourRegistry();

			// Register with descriptor ids whose natural (alphabetical/hash) order differs from registration order,
			// to ensure ordering tracks registration rather than the key.
			var expected = new List<GameBehaviour>();
			foreach (var id in new[] { "zzz", "aaa", "mmm", "bbb" })
			{
				var behaviour = MakeBehaviour("player");
				registry.Register(new BehaviourDescriptor(id, "move"), behaviour);
				expected.Add(behaviour);
			}

			var result = registry.GetByEntityTagAndBehaviourId("player", "move");

			CollectionAssert.AreEqual(expected, result);
		}
	}
}
