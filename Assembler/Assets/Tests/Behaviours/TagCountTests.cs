using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Assembler.Behaviours;
using Assembler.Behaviours.AI;
using Assembler.Behaviours.Spawners;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Behaviours
{
	public class TagCountTests
	{
		private static readonly IReadOnlyDictionary<string, string> NoRename = new Dictionary<string, string>();

		private readonly List<GameObject> _spawned = new();

		[TearDown]
		public void TearDown()
		{
			foreach (var go in _spawned)
			{
				if (go != null)
				{
					UnityEngine.Object.DestroyImmediate(go);
				}
			}

			_spawned.Clear();
		}

		// Captures the context a trigger notifies it with, so a test can read the emitted `count` output.
		private sealed class Capture : IAmExecutable
		{
			public TriggerContext? Last { get; private set; }

			public void Execute(TriggerContext ctx) => Last = ctx;
		}

		private GameEntity Enemy(EntityQueryService query, string id, params string[] tags)
		{
			var resolvedTags = tags.Length == 0 ? new[] { "enemy" } : tags;
			var go = new GameObject(id);
			var entity = go.AddComponent<GameEntity>();
			entity.Id = id;
			entity.Tags = resolvedTags;
			entity.Query = query;
			query.Register(id, go.transform, resolvedTags);
			_spawned.Add(go);
			return entity;
		}

		private TagCount BuildCounter(EntityQueryService query, string tag, params Listener[] listeners)
		{
			var go = new GameObject("counter");
			_spawned.Add(go);

			var counter = go.AddComponent<TagCount>();
			counter.Query = query;
			counter.Initialise(new TagCountData("tc", new ValueProvider<string>(tag)), listeners);
			return counter;
		}

		[Test]
		public void EmitsLiveCountForTag()
		{
			var query = new EntityQueryService();
			Enemy(query, "e1");
			Enemy(query, "e2");
			Enemy(query, "e3");
			Enemy(query, "ally", "ally");

			var capture = new Capture();
			var counter = BuildCounter(query, "enemy", new DirectListener(capture, NoRename));

			counter.Execute(TriggerContext.Empty);

			Assert.AreEqual(3, capture.Last!.Get<int>("count"));
		}

		[Test]
		public void PreservesUpstreamOutputsAlongsideCount()
		{
			var query = new EntityQueryService();
			Enemy(query, "e1");

			var capture = new Capture();
			var counter = BuildCounter(query, "enemy", new DirectListener(capture, NoRename));

			// The trigger that fires the recount already carried an output; it must survive downstream next to `count`.
			counter.Execute(TriggerContext.New("source_id", "e1"));

			Assert.AreEqual(1, capture.Last!.Get<int>("count"));
			Assert.AreEqual("e1", capture.Last!.Get<string>("source_id"));
		}

		[Test]
		public void EmitsZeroWhenNoEntityCarriesTag()
		{
			var query = new EntityQueryService();
			Enemy(query, "ally", "ally");

			var capture = new Capture();
			var counter = BuildCounter(query, "enemy", new DirectListener(capture, NoRename));

			counter.Execute(TriggerContext.Empty);

			Assert.AreEqual(0, capture.Last!.Get<int>("count"));
		}

		[Test]
		public void RecountWiredAfterDestroyExcludesTheJustKilledEntitySameFrame()
		{
			// Object.Destroy is disallowed in edit mode and logs an error; the eviction it is paired with is
			// synchronous regardless, which is exactly what this test asserts.
			LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));

			var query = new EntityQueryService();
			var doomed = Enemy(query, "doomed");
			Enemy(query, "survivor");

			var capture = new Capture();
			var counter = BuildCounter(query, "enemy", new DirectListener(capture, NoRename));

			// Wire the enemy's own `destroy` to recount: destroy self, then notify the tag counter — the exact
			// "enemy dies → how many enemies remain?" chain that must NOT count the corpse.
			var destroy = doomed.gameObject.AddComponent<DestroyBehaviour>();
			destroy.SetEntity(doomed);
			destroy.Initialise(new DestroyData("d"), new Listener[] { new DirectListener(counter, NoRename) });

			destroy.Execute(TriggerContext.Empty);

			// The dying entity left the index synchronously, so both the index and the recount read the survivor only.
			Assert.AreEqual(1, query.CountByTag("enemy"));
			Assert.AreEqual(1, capture.Last!.Get<int>("count"));
		}
	}
}
