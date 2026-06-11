using System;
using System.Collections.Generic;
using Assembler.Behaviours;
using Assembler.Behaviours.AI;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	public class PerceiveAllTests
	{
		private sealed class FakeClock : IGameClock
		{
			public float DeltaTime { get; set; }
			public float UnscaledDeltaTime { get; set; }
			public double Time { get; set; }
			public int FrameCount { get; set; }
			public float TimeScale { get; set; } = 1f;
			public bool IsPaused { get; set; }
			public void Pause() { }
			public void Resume() { }
			public void Step(int frames = 1) { }
		}

		private GameObject _seer = null!;
		private readonly List<GameObject> _spawned = new();

		[TearDown]
		public void TearDown()
		{
			if (_seer != null)
			{
				UnityEngine.Object.DestroyImmediate(_seer);
			}

			foreach (var go in _spawned)
			{
				if (go != null)
				{
					UnityEngine.Object.DestroyImmediate(go);
				}
			}

			_spawned.Clear();
		}

		private PerceiveAll Build(EntityQueryService query, PerceiveAllData data, FakeClock? clock = null)
		{
			_seer = new GameObject("seer");
			// The factory wires every behaviour to its owning entity; mirror that so PerceiveAll can read its own
			// entity id (to exclude itself from scans) via GameBehaviour.Entity.
			var entity = _seer.AddComponent<GameEntity>();
			entity.Id = "seer";
			entity.Tags = Array.Empty<string>();

			var perceive = _seer.AddComponent<PerceiveAll>();
			perceive.SetEntity(entity);
			perceive.Query = query;
			perceive.Sight = new LineOfSightService();
			perceive.Clock = clock ?? new FakeClock();
			perceive.Initialise(data, Array.Empty<Listener>());

			return perceive;
		}

		private static PerceiveAllData Data(
			IValueProvider<List<Vector3>> positions,
			IValueProvider<List<string>> ids,
			IValueProvider<List<Vector3>> velocities,
			IWriteValueProvider<int> count,
			float radius = 10f,
			IValueProvider<float>? coneAngle = null,
			IValueProvider<Vector3>? forward = null,
			bool requireLineOfSight = false,
			string obstacles = "") =>
			new("p",
				new ValueProvider<string>("enemy"),
				new ValueProvider<float>(radius),
				coneAngle ?? NullValueProvider<float>.Instance,
				forward ?? NullValueProvider<Vector3>.Instance,
				new ValueProvider<bool>(requireLineOfSight),
				new ValueProvider<string>(obstacles),
				new ValueProvider<float>(0f),
				positions, ids, velocities, count);

		private GameObject Enemy(EntityQueryService query, string id, Vector3 position, params string[] tags)
		{
			var resolvedTags = tags.Length == 0 ? new[] { "enemy" } : tags;
			var go = new GameObject(id) { transform = { position = position } };
			var entity = go.AddComponent<GameEntity>();
			entity.Id = id;
			entity.Tags = resolvedTags;
			query.Register(id, go.transform, resolvedTags);
			_spawned.Add(go);
			return go;
		}

		[Test]
		public void DetectsAllWithinRadius()
		{
			var query = new EntityQueryService();
			Enemy(query, "e1", new Vector3(2, 0, 0));
			Enemy(query, "e2", new Vector3(0, 3, 0));
			Enemy(query, "e3", new Vector3(50, 0, 0)); // out of range

			var positions = new ValueProvider<List<Vector3>>(new List<Vector3>());
			var ids = new ValueProvider<List<string>>(new List<string>());
			var count = new ValueProvider<int>(0);

			var perceive = Build(query,
				Data(positions, ids, NullValueProvider<List<Vector3>>.Instance, count));
			perceive.Execute(TriggerContext.Empty);

			Assert.AreEqual(new[] { "e1", "e2" }, ids.Get(TriggerContext.Empty).ToArray());
			Assert.AreEqual(new[] { new Vector3(2, 0, 0), new Vector3(0, 3, 0) },
				positions.Get(TriggerContext.Empty).ToArray());
			Assert.AreEqual(2, count.Get(TriggerContext.Empty));
		}

		[Test]
		public void ConeFiltersToForwardArc()
		{
			var query = new EntityQueryService();
			Enemy(query, "front", new Vector3(3, 0, 0));
			Enemy(query, "behind", new Vector3(-3, 0, 0));

			var ids = new ValueProvider<List<string>>(new List<string>());

			var perceive = Build(query,
				Data(NullValueProvider<List<Vector3>>.Instance, ids, NullValueProvider<List<Vector3>>.Instance,
					NullValueProvider<int>.Instance,
					coneAngle: new ValueProvider<float>(90f),
					forward: new ValueProvider<Vector3>(Vector3.right)));
			perceive.Execute(TriggerContext.Empty);

			Assert.AreEqual(new[] { "front" }, ids.Get(TriggerContext.Empty).ToArray());
		}

		[Test]
		public void LineOfSightGatesBlockedTargets()
		{
			var query = new EntityQueryService();
			Enemy(query, "blocked", new Vector3(5, 0, 0));
			Enemy(query, "visible", new Vector3(0, 5, 0));

			// A wall straddles the line from the seer (origin) to "blocked" but not to "visible".
			var wall = new GameObject("wall") { transform = { position = new Vector3(2.5f, 0, 0) } };
			var wallEntity = wall.AddComponent<GameEntity>();
			wallEntity.Id = "wall";
			wallEntity.Tags = new[] { "wall" };
			wall.AddComponent<BoxCollider>();
			_spawned.Add(wall);
			UnityEngine.Physics.SyncTransforms();

			var ids = new ValueProvider<List<string>>(new List<string>());

			var perceive = Build(query,
				Data(NullValueProvider<List<Vector3>>.Instance, ids, NullValueProvider<List<Vector3>>.Instance,
					NullValueProvider<int>.Instance,
					requireLineOfSight: true, obstacles: "wall"));
			perceive.Execute(TriggerContext.Empty);

			Assert.AreEqual(new[] { "visible" }, ids.Get(TriggerContext.Empty).ToArray());
		}

		[Test]
		public void ClearsAndRepopulatesEachScan()
		{
			var query = new EntityQueryService();
			var first = Enemy(query, "e1", new Vector3(2, 0, 0));
			Enemy(query, "e2", new Vector3(0, 2, 0));

			var positions = new ValueProvider<List<Vector3>>(new List<Vector3>());
			var ids = new ValueProvider<List<string>>(new List<string>());

			var perceive = Build(query,
				Data(positions, ids, NullValueProvider<List<Vector3>>.Instance, NullValueProvider<int>.Instance));
			perceive.Execute(TriggerContext.Empty);

			Assert.AreEqual(new[] { "e1", "e2" }, ids.Get(TriggerContext.Empty).ToArray());

			// e1 leaves range, a new e3 enters: the next scan must reflect only the current neighbourhood.
			first.transform.position = new Vector3(50, 0, 0);
			Enemy(query, "e3", new Vector3(1, 1, 0));
			perceive.Execute(TriggerContext.Empty);

			Assert.AreEqual(new[] { "e2", "e3" }, ids.Get(TriggerContext.Empty).ToArray());
			Assert.AreEqual(new[] { new Vector3(0, 2, 0), new Vector3(1, 1, 0) },
				positions.Get(TriggerContext.Empty).ToArray());
		}

		[Test]
		public void EmptyResultClearsLists()
		{
			var query = new EntityQueryService();
			Enemy(query, "far", new Vector3(50, 0, 0)); // out of range

			// Pre-seed the outputs with stale data to prove a scan clears them.
			var positions = new ValueProvider<List<Vector3>>(new List<Vector3> { new(9, 9, 9) });
			var ids = new ValueProvider<List<string>>(new List<string> { "stale" });
			var count = new ValueProvider<int>(7);

			var perceive = Build(query,
				Data(positions, ids, NullValueProvider<List<Vector3>>.Instance, count));
			perceive.Execute(TriggerContext.Empty);

			Assert.IsEmpty(positions.Get(TriggerContext.Empty));
			Assert.IsEmpty(ids.Get(TriggerContext.Empty));
			Assert.AreEqual(0, count.Get(TriggerContext.Empty));
		}

		[Test]
		public void VelocitiesAreFiniteDifferencedBetweenScans()
		{
			var query = new EntityQueryService();
			var mover = Enemy(query, "mover", new Vector3(2, 0, 0));

			var clock = new FakeClock { Time = 0 };
			var velocities = new ValueProvider<List<Vector3>>(new List<Vector3>());

			var perceive = Build(query,
				Data(NullValueProvider<List<Vector3>>.Instance, NullValueProvider<List<string>>.Instance,
					velocities, NullValueProvider<int>.Instance),
				clock);

			// First scan establishes the baseline: no prior sample, so velocity is zero.
			perceive.Execute(TriggerContext.Empty);
			Assert.AreEqual(new[] { Vector3.zero }, velocities.Get(TriggerContext.Empty).ToArray());

			// Advance the clock half a second and move the neighbour two units in x: velocity = 2 / 0.5 = 4.
			clock.Time = 0.5;
			mover.transform.position = new Vector3(4, 0, 0);
			perceive.Execute(TriggerContext.Empty);

			var velocity = velocities.Get(TriggerContext.Empty);
			Assert.AreEqual(1, velocity.Count);
			Assert.That(velocity[0].x, Is.EqualTo(4f).Within(1e-3f));
			Assert.That(velocity[0].y, Is.EqualTo(0f).Within(1e-3f));
		}

		[Test]
		public void OmittedOutputsAreNoOp()
		{
			var query = new EntityQueryService();
			Enemy(query, "e1", new Vector3(2, 0, 0));

			var count = new ValueProvider<int>(0);

			// Only Count is wired; the three list outputs are null-objects and must be skipped without throwing.
			var perceive = Build(query,
				Data(NullValueProvider<List<Vector3>>.Instance, NullValueProvider<List<string>>.Instance,
					NullValueProvider<List<Vector3>>.Instance, count));

			Assert.DoesNotThrow(() => perceive.Execute(TriggerContext.Empty));
			Assert.AreEqual(1, count.Get(TriggerContext.Empty));
		}
	}
}
