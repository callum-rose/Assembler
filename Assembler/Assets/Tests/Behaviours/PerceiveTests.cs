using System;
using Assembler.Behaviours;
using Assembler.Behaviours.AI;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	public class PerceiveTests
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
		private GameObject _enemy = null!;

		[TearDown]
		public void TearDown()
		{
			if (_seer != null)
			{
				UnityEngine.Object.DestroyImmediate(_seer);
			}

			if (_enemy != null)
			{
				UnityEngine.Object.DestroyImmediate(_enemy);
			}
		}

		private (Perceive perceive, ValueProvider<string> id, ValueProvider<Vector3> pos,
			ValueProvider<bool> has, ValueProvider<Vector3> lastKnown) Build(EntityQueryService query)
		{
			_seer = new GameObject("seer");
			// The factory wires every behaviour to its owning entity; mirror that so Perceive can read its own
			// entity id (to exclude itself from scans) via GameBehaviour.Entity.
			var entity = _seer.AddComponent<GameEntity>();
			entity.Id = "seer";
			var perceive = _seer.AddComponent<Perceive>();
			perceive.SetEntity(entity);
			perceive.Query = query;
			perceive.Sight = new LineOfSightService();
			perceive.Clock = new FakeClock();

			var id = new ValueProvider<string>(string.Empty);
			var pos = new ValueProvider<Vector3>(Vector3.zero);
			var has = new ValueProvider<bool>(false);
			var lastKnown = new ValueProvider<Vector3>(Vector3.zero);

			perceive.Initialise(new PerceiveData("p",
				new ValueProvider<string>("enemy"),
				new ValueProvider<float>(10f),
				NullValueProvider<float>.Instance,
				NullValueProvider<Vector3>.Instance,
				new ValueProvider<bool>(false),
				new ValueProvider<string>(string.Empty),
				new ValueProvider<float>(0f),
				id, pos, has, lastKnown), Array.Empty<Listener>());

			return (perceive, id, pos, has, lastKnown);
		}

		[Test]
		public void WritesAllOutputsWhenTargetVisible()
		{
			var query = new EntityQueryService();
			_enemy = new GameObject("enemy") { transform = { position = new Vector3(3, 0, 0) } };
			query.Register("enemy", _enemy.transform, new[] { "enemy" });

			var (perceive, id, pos, has, lastKnown) = Build(query);
			perceive.Execute(TriggerContext.Empty);

			Assert.AreEqual("enemy", id.Get(TriggerContext.Empty));
			Assert.IsTrue(has.Get(TriggerContext.Empty));
			Assert.AreEqual(new Vector3(3, 0, 0), pos.Get(TriggerContext.Empty));
			Assert.AreEqual(new Vector3(3, 0, 0), lastKnown.Get(TriggerContext.Empty));
		}

		[Test]
		public void ExcludesSelfWhenSharingPerceivedTag()
		{
			var query = new EntityQueryService();
			_enemy = new GameObject("enemy") { transform = { position = new Vector3(3, 0, 0) } };
			query.Register("enemy", _enemy.transform, new[] { "enemy" });

			var (perceive, id, pos, has, _) = Build(query);

			// The seer also carries the tag it perceives and sits at the origin (distance 0); without
			// self-exclusion it would always detect itself instead of the real enemy.
			query.Register("seer", _seer.transform, new[] { "enemy" });
			perceive.Execute(TriggerContext.Empty);

			Assert.IsTrue(has.Get(TriggerContext.Empty));
			Assert.AreEqual("enemy", id.Get(TriggerContext.Empty));
			Assert.AreEqual(new Vector3(3, 0, 0), pos.Get(TriggerContext.Empty));
		}

		[Test]
		public void RetainsLastKnownWhenTargetLost()
		{
			var query = new EntityQueryService();
			_enemy = new GameObject("enemy") { transform = { position = new Vector3(3, 0, 0) } };
			query.Register("enemy", _enemy.transform, new[] { "enemy" });

			var (perceive, _, _, has, lastKnown) = Build(query);
			perceive.Execute(TriggerContext.Empty);

			// Target moves out of detection range.
			_enemy.transform.position = new Vector3(50, 0, 0);
			perceive.Execute(TriggerContext.Empty);

			Assert.IsFalse(has.Get(TriggerContext.Empty));
			Assert.AreEqual(new Vector3(3, 0, 0), lastKnown.Get(TriggerContext.Empty),
				"last known position must be retained after losing the target");
		}
	}
}
