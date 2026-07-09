using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Behaviours.Spawners;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Tests.Behaviours
{
	public class SpawnerBehaviourTests : BehaviourTestFixture
	{
		private sealed class RecordingSpawner : IEntitySpawner
		{
			public List<string> SpawnedTemplateIds { get; } = new();

			public void Spawn(string templateId, Vector3 position, Vector3 rotation,
				IReadOnlyDictionary<string, object> parameters) => SpawnedTemplateIds.Add(templateId);
		}

		[SetUp]
		public void Seed() => Random.InitState(12345);

		private (SpawnerBehaviour behaviour, RecordingSpawner spawner) NewSpawner(SpawnerData data)
		{
			var go = Track(new GameObject("SpawnerTestObject"));
			var behaviour = go.AddComponent<SpawnerBehaviour>();
			var spawner = new RecordingSpawner();
			behaviour.Spawner = spawner;
			behaviour.Initialise(data, Array.Empty<Listener>());
			return (behaviour, spawner);
		}

		private static SpawnerData Data(
			IValueProvider<string>? templateId = null,
			IReadOnlyList<SpawnTemplate>? templates = null,
			IValueProvider<string>? selection = null) =>
			new("spawner",
				templateId ?? NullValueProvider<string>.Instance,
				templates ?? Array.Empty<SpawnTemplate>(),
				selection ?? NullValueProvider<string>.Instance,
				new ValueProvider<Vector3>(Vector3.zero),
				new ValueProvider<Vector3>(Vector3.zero),
				new Dictionary<string, IValueProvider>());

		private static SpawnTemplate Template(string id, float weight = 1f) =>
			new(id, new ValueProvider<float>(weight));

		[Test]
		public void FallsBackToTemplateIdWhenNoTemplatesListed()
		{
			var (behaviour, spawner) = NewSpawner(Data(templateId: new ValueProvider<string>("solo")));
			behaviour.Execute(TriggerContext.Empty);
			behaviour.Execute(TriggerContext.Empty);
			CollectionAssert.AreEqual(new[] { "solo", "solo" }, spawner.SpawnedTemplateIds);
		}

		[Test]
		public void SequentialSelectionCyclesThroughTemplatesInOrder()
		{
			var data = Data(
				templates: new[] { Template("a"), Template("b") },
				selection: new ValueProvider<string>("sequential"));
			var (behaviour, spawner) = NewSpawner(data);
			for (int i = 0; i < 4; i++)
			{
				behaviour.Execute(TriggerContext.Empty);
			}

			CollectionAssert.AreEqual(new[] { "a", "b", "a", "b" }, spawner.SpawnedTemplateIds);
		}

		[Test]
		public void RandomSelectionOnlySpawnsPositivelyWeightedTemplates()
		{
			var data = Data(templates: new[] { Template("zero", 0f), Template("one", 1f) });
			var (behaviour, spawner) = NewSpawner(data);
			for (int i = 0; i < 50; i++)
			{
				behaviour.Execute(TriggerContext.Empty);
			}

			CollectionAssert.DoesNotContain(spawner.SpawnedTemplateIds, "zero");
			Assert.That(spawner.SpawnedTemplateIds, Has.All.EqualTo("one"));
		}

		[Test]
		public void RandomSelectionCoversAllTemplatesAndOnlyThem()
		{
			var data = Data(templates: new[] { Template("a"), Template("b") });
			var (behaviour, spawner) = NewSpawner(data);
			for (int i = 0; i < 200; i++)
			{
				behaviour.Execute(TriggerContext.Empty);
			}

			// Every spawn is one of the two templates, and both appear over many spawns.
			Assert.That(spawner.SpawnedTemplateIds.Distinct(), Is.EquivalentTo(new[] { "a", "b" }));
		}

		[Test]
		public void ThrowsWhenNeitherTemplatesNorTemplateIdSet()
		{
			var (behaviour, _) = NewSpawner(Data());
			Assert.Throws<InvalidOperationException>(() => behaviour.Execute(TriggerContext.Empty));
		}
	}
}
