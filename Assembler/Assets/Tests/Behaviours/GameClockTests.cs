using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Behaviours.Movement;
using Assembler.Behaviours.Rotation;
using Assembler.Behaviours.Time;
using Assembler.Behaviours.Triggers.Timing;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	public class GameClockTests
	{
		// Hand-driven IGameClock for unit tests: every property is settable, and Advance simulates a
		// frame tick (accumulating Time and FrameCount). Tick() is not on the interface, so fakes like
		// this need not implement it.
		private sealed class FakeGameClock : IGameClock
		{
			public float DeltaTime { get; set; }
			public float UnscaledDeltaTime { get; set; }
			public double Time { get; set; }
			public int FrameCount { get; set; }
			public float TimeScale { get; set; } = 1f;
			public bool IsPaused { get; set; }

			public void Pause()
			{
				IsPaused = true;
				DeltaTime = 0f;
			}

			public void Resume() => IsPaused = false;

			public void Step(int frames = 1) { }

			public void Advance(float seconds)
			{
				DeltaTime = seconds;
				Time += seconds;
				FrameCount++;
			}
		}

		private sealed class ActionListener : Listener
		{
			private readonly Action<TriggerContext> _action;

			public ActionListener(Action<TriggerContext> action)
				: base(new Dictionary<string, string>())
			{
				_action = action;
			}

			public override void Notify(TriggerContext ctx) => _action(Prepare(ctx));

			public override IEnumerable<GameBehaviour> DebugTargets() => Enumerable.Empty<GameBehaviour>();
		}

		private static T NewBehaviour<T>(GameObject go, FakeGameClock clock) where T : GameBehaviour
		{
			var behaviour = go.AddComponent<T>();
			if (behaviour is INeedsGameClock needsClock)
			{
				needsClock.Clock = clock;
			}

			return behaviour;
		}

		// ---- Motion: Velocity ----

		[Test]
		public void Velocity_MovesByVelocityTimesDelta()
		{
			var go = new GameObject("velocity");
			try
			{
				var fake = new FakeGameClock { DeltaTime = 0.5f };
				var velocity = NewBehaviour<Velocity>(go, fake);
				velocity.Initialise(new VelocityData("v", new ValueProvider<Vector3>(new Vector3(2f, 0f, 0f))),
					Array.Empty<Listener>());

				velocity.Execute(TriggerContext.Empty);

				Assert.AreEqual(new Vector3(1f, 0f, 0f), go.transform.position);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void Velocity_FrozenWhenPaused()
		{
			var go = new GameObject("velocity");
			try
			{
				var fake = new FakeGameClock();
				fake.Pause();
				var velocity = NewBehaviour<Velocity>(go, fake);
				velocity.Initialise(new VelocityData("v", new ValueProvider<Vector3>(new Vector3(5f, 5f, 5f))),
					Array.Empty<Listener>());

				velocity.Execute(TriggerContext.Empty);

				Assert.AreEqual(Vector3.zero, go.transform.position);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void Velocity_HalfDeltaMovesHalfDistance()
		{
			var go = new GameObject("velocity");
			try
			{
				var fake = new FakeGameClock { DeltaTime = 0.25f };
				var velocity = NewBehaviour<Velocity>(go, fake);
				velocity.Initialise(new VelocityData("v", new ValueProvider<Vector3>(new Vector3(4f, 0f, 0f))),
					Array.Empty<Listener>());

				velocity.Execute(TriggerContext.Empty);

				Assert.AreEqual(new Vector3(1f, 0f, 0f), go.transform.position);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		// ---- Motion: Acceleration ----

		[Test]
		public void Acceleration_IntegratesVelocityOverTwoExecutes()
		{
			var go = new GameObject("acceleration");
			try
			{
				var fake = new FakeGameClock { DeltaTime = 1f };
				var acceleration = NewBehaviour<Acceleration>(go, fake);
				acceleration.Initialise(new AccelerationData("a", new ValueProvider<Vector3>(new Vector3(0f, 1f, 0f)),
						NullValueProvider<Vector3>.Instance),
					Array.Empty<Listener>());

				// Frame 1: v = (0,1,0); pos += v*dt = (0,1,0)
				acceleration.Execute(TriggerContext.Empty);
				Assert.AreEqual(new Vector3(0f, 1f, 0f), go.transform.position);

				// Frame 2: v = (0,2,0); pos += v*dt = (0,3,0)
				acceleration.Execute(TriggerContext.Empty);
				Assert.AreEqual(new Vector3(0f, 3f, 0f), go.transform.position);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void Acceleration_FrozenWhenPaused()
		{
			var go = new GameObject("acceleration");
			try
			{
				var fake = new FakeGameClock();
				fake.Pause();
				var acceleration = NewBehaviour<Acceleration>(go, fake);
				acceleration.Initialise(new AccelerationData("a", new ValueProvider<Vector3>(new Vector3(0f, 9f, 0f)),
						NullValueProvider<Vector3>.Instance),
					Array.Empty<Listener>());

				acceleration.Execute(TriggerContext.Empty);
				acceleration.Execute(TriggerContext.Empty);

				Assert.AreEqual(Vector3.zero, go.transform.position);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		// ---- Shared velocity: Acceleration (shared mode) + Velocity integrator ----

		[Test]
		public void Acceleration_SharedMode_WritesVelocityAndLeavesPositionToIntegrator()
		{
			var go = new GameObject("shared velocity");
			try
			{
				var fake = new FakeGameClock { DeltaTime = 1f };

				// One shared, writable velocity variable that both behaviours touch.
				var shared = new ValueProvider<Vector3>(Vector3.zero);

				var acceleration = NewBehaviour<Acceleration>(go, fake);
				acceleration.Initialise(
					new AccelerationData("a", new ValueProvider<Vector3>(new Vector3(0f, 10f, 0f)), shared),
					Array.Empty<Listener>());

				acceleration.Execute(TriggerContext.Empty);

				// Shared mode: it integrates into the shared velocity but does NOT move the entity.
				Assert.AreEqual(new Vector3(0f, 10f, 0f), shared.Get(TriggerContext.Empty));
				Assert.AreEqual(Vector3.zero, go.transform.position);

				// The Velocity integrator, fed the SAME provider, moves the entity by vel*dt.
				var velocity = NewBehaviour<Velocity>(go, fake);
				velocity.Initialise(new VelocityData("v", shared), Array.Empty<Listener>());

				velocity.Execute(TriggerContext.Empty);

				Assert.AreEqual(new Vector3(0f, 10f, 0f), go.transform.position);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		// ---- Drag (exponential decay on shared velocity) ----

		[Test]
		public void Drag_DecaysVelocityExponentially()
		{
			var go = new GameObject("drag");
			try
			{
				var fake = new FakeGameClock { DeltaTime = 0.5f };
				var shared = new ValueProvider<Vector3>(new Vector3(10f, 0f, 0f));

				var drag = NewBehaviour<DragBehaviour>(go, fake);
				drag.Initialise(new DragData("d", shared, new ValueProvider<float>(2f)), Array.Empty<Listener>());

				drag.Execute(TriggerContext.Empty);

				// magnitude == 10 * exp(-2 * 0.5) == 10 * exp(-1)
				Assert.AreEqual(10f * Mathf.Exp(-1f), shared.Get(TriggerContext.Empty).magnitude, 1e-4f);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void Drag_RequiresWritableVelocity()
		{
			var go = new GameObject("drag");
			try
			{
				var fake = new FakeGameClock { DeltaTime = 0.5f };
				var drag = NewBehaviour<DragBehaviour>(go, fake);

				Assert.Throws<InvalidOperationException>(() =>
					drag.Initialise(new DragData("d", NullValueProvider<Vector3>.Instance, new ValueProvider<float>(2f)),
						Array.Empty<Listener>()));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		// ---- MoveTowards ----

		[Test]
		public void MoveTowards_StepsTowardTargetAtSpeed()
		{
			var go = new GameObject("move towards");
			try
			{
				var fake = new FakeGameClock { DeltaTime = 0.5f };
				var move = NewBehaviour<MoveTowards>(go, fake);
				move.Initialise(new MoveTowardsData("m",
					new ValueProvider<Vector3>(new Vector3(10f, 0f, 0f)),
					new ValueProvider<float>(2f)), Array.Empty<Listener>());

				move.Execute(TriggerContext.Empty); // 2 units/s * 0.5s = 1 unit toward (10,0,0)

				Assert.AreEqual(new Vector3(1f, 0f, 0f), go.transform.position);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void MoveTowards_DoesNotOvershootTarget()
		{
			var go = new GameObject("move towards");
			try
			{
				var fake = new FakeGameClock { DeltaTime = 1f };
				go.transform.position = new Vector3(9.5f, 0f, 0f);

				var move = NewBehaviour<MoveTowards>(go, fake);
				move.Initialise(new MoveTowardsData("m",
					new ValueProvider<Vector3>(new Vector3(10f, 0f, 0f)),
					new ValueProvider<float>(100f)), Array.Empty<Listener>()); // step far exceeds remaining 0.5

				move.Execute(TriggerContext.Empty);

				Assert.AreEqual(new Vector3(10f, 0f, 0f), go.transform.position);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		// ---- SmoothMove ----

		[Test]
		public void SmoothMove_EasesTowardTargetWithoutOvershoot()
		{
			var go = new GameObject("smooth move");
			try
			{
				var fake = new FakeGameClock { DeltaTime = 0.1f };
				var smooth = NewBehaviour<SmoothMove>(go, fake);
				smooth.Initialise(new SmoothMoveData("s",
					new ValueProvider<Vector3>(new Vector3(10f, 0f, 0f)),
					new ValueProvider<float>(1f)), Array.Empty<Listener>());

				smooth.Execute(TriggerContext.Empty);

				// Moved toward the target but nowhere near overshooting it.
				Assert.Greater(go.transform.position.x, 0f);
				Assert.Less(go.transform.position.x, 10f);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		// ---- Motion: AngularVelocity ----

		[Test]
		public void AngularVelocity_RotatesByAngularVelocityTimesDelta()
		{
			var go = new GameObject("angular");
			try
			{
				var fake = new FakeGameClock { DeltaTime = 0.1f };
				var angular = NewBehaviour<AngularVelocity>(go, fake);
				angular.Initialise(
					new AngularVelocityData("av", new ValueProvider<Vector3>(new Vector3(0f, 0f, 10f))),
					Array.Empty<Listener>());

				angular.Execute(TriggerContext.Empty);

				// 10 deg/s * 0.1s = 1 deg about z (small angle, no wrap ambiguity).
				Assert.AreEqual(1f, go.transform.eulerAngles.z, 1e-3f);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void AngularVelocity_FrozenWhenPaused()
		{
			var go = new GameObject("angular");
			try
			{
				var fake = new FakeGameClock();
				fake.Pause();
				var angular = NewBehaviour<AngularVelocity>(go, fake);
				angular.Initialise(
					new AngularVelocityData("av", new ValueProvider<Vector3>(new Vector3(0f, 0f, 90f))),
					Array.Empty<Listener>());

				angular.Execute(TriggerContext.Empty);

				Assert.AreEqual(Quaternion.identity, go.transform.rotation);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		// ---- Debounced trigger (clock.Time driven) ----

		[Test]
		public void Debounced_SuppressesWithinIntervalThenForwards()
		{
			var go = new GameObject("debounced");
			try
			{
				var fake = new FakeGameClock();
				var debounced = NewBehaviour<DebouncedTrigger>(go, fake);

				int fires = 0;
				debounced.Initialise(new DebouncedTriggerData("d", new ValueProvider<float>(1f)),
					new List<Listener> { new ActionListener(_ => fires++) });

				fake.Time = 0d;
				debounced.Execute(TriggerContext.Empty); // first: forwarded
				fake.Time = 0.5d;
				debounced.Execute(TriggerContext.Empty); // within interval: suppressed
				fake.Time = 2.0d;
				debounced.Execute(TriggerContext.Empty); // past interval: forwarded

				Assert.AreEqual(2, fires);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void Debounced_StaysSuppressedWhileTimeFrozen()
		{
			var go = new GameObject("debounced");
			try
			{
				var fake = new FakeGameClock { Time = 0.5d };
				var debounced = NewBehaviour<DebouncedTrigger>(go, fake);

				int fires = 0;
				debounced.Initialise(new DebouncedTriggerData("d", new ValueProvider<float>(1f)),
					new List<Listener> { new ActionListener(_ => fires++) });

				debounced.Execute(TriggerContext.Empty); // first: forwarded
														 // Time frozen (e.g. paused): every subsequent trigger is within the interval.
				debounced.Execute(TriggerContext.Empty);
				debounced.Execute(TriggerContext.Empty);

				Assert.AreEqual(1, fires);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		// ---- Throttled trigger (clock.Time driven) ----

		[Test]
		public void Throttled_DropsFasterThanRate()
		{
			var go = new GameObject("throttled");
			try
			{
				var fake = new FakeGameClock();
				var throttled = NewBehaviour<ThrottledTrigger>(go, fake);

				int fires = 0;
				throttled.Initialise(new ThrottledTriggerData("t", new ValueProvider<float>(2f)), // min interval 0.5s
					new List<Listener> { new ActionListener(_ => fires++) });

				fake.Time = 0d;
				throttled.Execute(TriggerContext.Empty); // forwarded
				fake.Time = 0.2d;
				throttled.Execute(TriggerContext.Empty); // dropped
				fake.Time = 0.6d;
				throttled.Execute(TriggerContext.Empty); // forwarded

				Assert.AreEqual(2, fires);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void Throttled_RateZeroDropsAll()
		{
			var go = new GameObject("throttled");
			try
			{
				var fake = new FakeGameClock();
				var throttled = NewBehaviour<ThrottledTrigger>(go, fake);

				int fires = 0;
				throttled.Initialise(new ThrottledTriggerData("t", new ValueProvider<float>(0f)),
					new List<Listener> { new ActionListener(_ => fires++) });

				throttled.Execute(TriggerContext.Empty);
				fake.Time = 100d;
				throttled.Execute(TriggerContext.Empty);

				Assert.AreEqual(0, fires);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		// ---- set timescale behaviour ----

		[Test]
		public void SetTimeScale_SetsClockTimeScale()
		{
			var go = new GameObject("set timescale");
			try
			{
				var fake = new FakeGameClock();
				var setScale = NewBehaviour<SetTimeScale>(go, fake);
				setScale.Initialise(new SetTimeScaleData("s", new ValueProvider<float>(0.5f)),
					Array.Empty<Listener>());

				setScale.Execute(TriggerContext.Empty);
				Assert.AreEqual(0.5f, fake.TimeScale);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void SetTimeScale_ZeroPausesByScale()
		{
			var go = new GameObject("set timescale");
			try
			{
				var fake = new FakeGameClock();
				var setScale = NewBehaviour<SetTimeScale>(go, fake);
				setScale.Initialise(new SetTimeScaleData("s", new ValueProvider<float>(0f)),
					Array.Empty<Listener>());

				setScale.Execute(TriggerContext.Empty);
				Assert.AreEqual(0f, fake.TimeScale);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		// ---- RealtimeGameClock ----

		[Test]
		public void RealtimeGameClock_TimeScaleClampsNegative()
		{
			var clock = new RealtimeGameClock { TimeScale = -5f };
			Assert.AreEqual(0f, clock.TimeScale);
		}

		[Test]
		public void RealtimeGameClock_UnpausedTickAdvancesFrameCount()
		{
			var clock = new RealtimeGameClock();
			Assert.AreEqual(0, clock.FrameCount);

			clock.Tick();
			clock.Tick();

			Assert.AreEqual(2, clock.FrameCount);
		}

		[Test]
		public void RealtimeGameClock_PausedTickYieldsZeroDeltaAndFrozenFrameCount()
		{
			var clock = new RealtimeGameClock();
			clock.Tick();
			int frameBeforePause = clock.FrameCount;

			clock.Pause();
			clock.Tick();
			clock.Tick();

			Assert.AreEqual(0f, clock.DeltaTime);
			Assert.AreEqual(frameBeforePause, clock.FrameCount);
			Assert.IsTrue(clock.IsPaused);
		}

		[Test]
		public void RealtimeGameClock_StepAdvancesExactlyOneFrameWhilePaused()
		{
			var clock = new RealtimeGameClock();
			clock.Pause();
			clock.Tick();
			int frozen = clock.FrameCount;

			clock.Step();
			clock.Tick(); // consumes the queued step: advances one frame
			clock.Tick(); // no step queued: frozen again

			Assert.AreEqual(frozen + 1, clock.FrameCount);
			Assert.IsTrue(clock.IsPaused);
		}

		[Test]
		public void RealtimeGameClock_StepQueuesMultipleFrames()
		{
			var clock = new RealtimeGameClock();
			clock.Pause();
			clock.Tick();
			int frozen = clock.FrameCount;

			clock.Step(3);
			clock.Tick();
			clock.Tick();
			clock.Tick();
			clock.Tick(); // fourth tick: queue empty, frozen

			Assert.AreEqual(frozen + 3, clock.FrameCount);
		}

		[Test]
		public void RealtimeGameClock_StepIgnoredWhenNotPaused()
		{
			var clock = new RealtimeGameClock();
			clock.Step(5); // queued but never consumed while running

			clock.Tick();
			Assert.AreEqual(1, clock.FrameCount);

			// The queued steps must not leak into a later pause.
			clock.Pause();
			clock.Tick();
			Assert.AreEqual(1, clock.FrameCount);
		}

		[Test]
		public void RealtimeGameClock_ResumeRestoresAdvance()
		{
			var clock = new RealtimeGameClock();
			clock.Pause();
			clock.Tick();
			int frozen = clock.FrameCount;

			clock.Resume();
			clock.Tick();

			Assert.AreEqual(frozen + 1, clock.FrameCount);
			Assert.IsFalse(clock.IsPaused);
		}

		// ---- WaitForGameSeconds ----

		[Test]
		public void WaitForGameSeconds_CompletesAfterAccumulatedDelta()
		{
			var fake = new FakeGameClock { DeltaTime = 0.5f };
			var wait = new WaitForGameSeconds(fake, 1f);

			Assert.IsTrue(wait.keepWaiting);  // elapsed 0.5 < 1
			Assert.IsFalse(wait.keepWaiting); // elapsed 1.0, done
		}

		[Test]
		public void WaitForGameSeconds_NeverCompletesWhilePaused()
		{
			var fake = new FakeGameClock();
			fake.Pause(); // DeltaTime == 0
			var wait = new WaitForGameSeconds(fake, 1f);

			for (int i = 0; i < 100; i++)
			{
				Assert.IsTrue(wait.keepWaiting);
			}
		}

		[Test]
		public void WaitForGameSeconds_ResumesAfterUnpause()
		{
			var fake = new FakeGameClock();
			fake.Pause();
			var wait = new WaitForGameSeconds(fake, 1f);

			Assert.IsTrue(wait.keepWaiting); // frozen, no progress

			fake.Resume();
			fake.DeltaTime = 1f;
			Assert.IsFalse(wait.keepWaiting); // elapsed 1.0, done
		}
	}
}
