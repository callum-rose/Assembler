using System;
using Assembler.Behaviours;
using Assembler.Behaviours.Time;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	/// <summary>
	/// Tests of the clock itself: the <c>set timescale</c> behaviour that drives it, the concrete
	/// <see cref="RealtimeGameClock"/> (pause/step/resume/frame-count semantics), and
	/// <see cref="WaitForGameSeconds"/>. Motion behaviours and rate-limiting triggers that merely read a
	/// clock live in <see cref="MotionBehaviourClockTests"/> and <see cref="RateLimitingTriggerTests"/>.
	/// </summary>
	public class GameClockTests : BehaviourTestFixture
	{
		// ---- set timescale behaviour ----

		[Test]
		public void SetTimeScale_SetsClockTimeScale()
		{
			var go = Track(new GameObject("set timescale"));
			var fake = new FakeGameClock();
			var setScale = NewBehaviour<SetTimeScale>(go, fake);
			setScale.Initialise(new SetTimeScaleData("s", new ValueProvider<float>(0.5f)),
				Array.Empty<Listener>());

			setScale.Execute(TriggerContext.Empty);
			Assert.AreEqual(0.5f, fake.TimeScale);
		}

		[Test]
		public void SetTimeScale_ZeroPausesByScale()
		{
			var go = Track(new GameObject("set timescale"));
			var fake = new FakeGameClock();
			var setScale = NewBehaviour<SetTimeScale>(go, fake);
			setScale.Initialise(new SetTimeScaleData("s", new ValueProvider<float>(0f)),
				Array.Empty<Listener>());

			setScale.Execute(TriggerContext.Empty);
			Assert.AreEqual(0f, fake.TimeScale);
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
