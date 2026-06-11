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
	public class PatrolTests
	{
		private sealed class FakeClock : IGameClock
		{
			public float DeltaTime { get; set; } = 0.1f;
			public float UnscaledDeltaTime { get; set; }
			public double Time { get; set; }
			public int FrameCount { get; set; }
			public float TimeScale { get; set; } = 1f;
			public bool IsPaused { get; set; }
			public void Pause() { }
			public void Resume() { }
			public void Step(int frames = 1) { }
		}

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

		private static PatrolData Data(
			List<Vector3> waypoints,
			bool loop = true,
			bool pingPong = false,
			float arriveRadius = 0.2f,
			float speed = 3f,
			IWriteValueProvider<Vector3>? output = null,
			IWriteValueProvider<int>? currentIndex = null) =>
			new("patrol",
				new ValueProvider<List<Vector3>>(waypoints),
				new ValueProvider<bool>(loop),
				new ValueProvider<bool>(pingPong),
				new ValueProvider<float>(arriveRadius),
				new ValueProvider<float>(speed),
				output ?? NullValueProvider<Vector3>.Instance,
				currentIndex ?? NullValueProvider<int>.Instance);

		private Patrol NewPatrol(PatrolData data, out GameObject go)
		{
			go = new GameObject("patrol");
			_spawned.Add(go);
			var patrol = go.AddComponent<Patrol>();
			patrol.Clock = new FakeClock();
			patrol.Initialise(data, Array.Empty<Listener>());
			return patrol;
		}

		// Standing the entity on each waypoint and ticking once should advance the index along the route; with
		// Loop off the index holds at the final waypoint.
		[Test]
		public void ForwardTraversal_AdvancesIndexAndHoldsAtEndWhenNotLooping()
		{
			var wps = new List<Vector3> { new(0, 0, 0), new(2, 0, 0), new(4, 0, 0) };
			var idx = new ValueProvider<int>(-1);
			var patrol = NewPatrol(
				Data(wps, loop: false, output: new ValueProvider<Vector3>(Vector3.zero), currentIndex: idx),
				out var go);

			go.transform.position = wps[0];
			patrol.Step();
			Assert.AreEqual(1, idx.Get(TriggerContext.Empty));

			go.transform.position = wps[1];
			patrol.Step();
			Assert.AreEqual(2, idx.Get(TriggerContext.Empty));

			// At the final waypoint with Loop off, the index stays put (one-shot path).
			go.transform.position = wps[2];
			patrol.Step();
			Assert.AreEqual(2, idx.Get(TriggerContext.Empty));
		}

		// Reaching the last waypoint with Loop on wraps the index back to the start.
		[Test]
		public void Loop_WrapsIndexBackToStart()
		{
			var wps = new List<Vector3> { new(0, 0, 0), new(2, 0, 0), new(4, 0, 0) };
			var idx = new ValueProvider<int>(-1);
			var patrol = NewPatrol(
				Data(wps, loop: true, output: new ValueProvider<Vector3>(Vector3.zero), currentIndex: idx),
				out var go);

			var seen = new List<int>();
			for (var i = 0; i < 4; i++)
			{
				go.transform.position = wps[idx.Get(TriggerContext.Empty) < 0 ? 0 : idx.Get(TriggerContext.Empty)];
				patrol.Step();
				seen.Add(idx.Get(TriggerContext.Empty));
			}

			// 0 -> 1 -> 2 -> wrap to 0 -> 1.
			CollectionAssert.AreEqual(new[] { 1, 2, 0, 1 }, seen);
		}

		// PingPong reverses direction at each end rather than wrapping.
		[Test]
		public void PingPong_ReversesDirectionAtEnds()
		{
			var wps = new List<Vector3> { new(0, 0, 0), new(2, 0, 0), new(4, 0, 0) };
			var idx = new ValueProvider<int>(0);
			var patrol = NewPatrol(
				Data(wps, loop: true, pingPong: true, output: new ValueProvider<Vector3>(Vector3.zero),
					currentIndex: idx),
				out var go);

			var seen = new List<int>();
			for (var i = 0; i < 5; i++)
			{
				go.transform.position = wps[idx.Get(TriggerContext.Empty)];
				patrol.Step();
				seen.Add(idx.Get(TriggerContext.Empty));
			}

			// 0 -> 1 -> 2 -> reverse to 1 -> 0 -> reverse to 1.
			CollectionAssert.AreEqual(new[] { 1, 2, 1, 0, 1 }, seen);
		}

		// A single-waypoint route has nowhere to advance to; the index stays at 0.
		[Test]
		public void SingleWaypoint_HoldsAtIndexZero()
		{
			var wps = new List<Vector3> { new(1, 1, 0) };
			var idx = new ValueProvider<int>(-1);
			var patrol = NewPatrol(
				Data(wps, loop: true, output: new ValueProvider<Vector3>(Vector3.zero), currentIndex: idx),
				out var go);

			go.transform.position = wps[0];
			for (var i = 0; i < 3; i++)
			{
				patrol.Step();
				Assert.AreEqual(0, idx.Get(TriggerContext.Empty));
			}
		}

		// An empty route is a no-op: no movement, no exception, index stays 0.
		[Test]
		public void EmptyList_IsNoOp()
		{
			var idx = new ValueProvider<int>(-1);
			var patrol = NewPatrol(Data(new List<Vector3>(), currentIndex: idx), out var go);

			go.transform.position = new Vector3(5, 5, 0);
			Assert.DoesNotThrow(() => patrol.Step());

			Assert.AreEqual(new Vector3(5, 5, 0), go.transform.position, "empty route must not move the entity");
			Assert.AreEqual(0, idx.Get(TriggerContext.Empty));
		}

		// With no Output bound, patrol integrates onto the transform directly and walks the route.
		[Test]
		public void MovesEntityDirectlyToReachWaypoints()
		{
			var wps = new List<Vector3> { new(1, 0, 0), new(3, 0, 0) };
			var patrol = NewPatrol(Data(wps, loop: false, arriveRadius: 0.2f, speed: 4f), out var go);
			((FakeClock)patrol.Clock).DeltaTime = 0.05f;

			go.transform.position = Vector3.zero;
			for (var i = 0; i < 400; i++)
			{
				patrol.Step();
			}

			// Reached the final waypoint and eased to a stop near it.
			Assert.Less(Vector3.Distance(go.transform.position, new Vector3(3, 0, 0)), 0.3f);
		}

		// When Output is bound, the desired velocity is written to the variable and the transform is left alone.
		[Test]
		public void WritesVelocityToOutputWithoutMovingTransform()
		{
			var wps = new List<Vector3> { new(10, 0, 0) };
			var velocity = new ValueProvider<Vector3>(Vector3.zero);
			var patrol = NewPatrol(Data(wps, loop: false, speed: 3f, output: velocity), out var go);

			go.transform.position = Vector3.zero;
			patrol.Step();

			// Far from the only waypoint, Arrive runs at full speed straight along +x.
			Assert.AreEqual(new Vector3(3, 0, 0), velocity.Get(TriggerContext.Empty));
			Assert.AreEqual(Vector3.zero, go.transform.position, "bound Output must not move the transform");
		}
	}
}
