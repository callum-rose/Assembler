using System.Collections.Generic;
using Assembler.Time;
using NUnit.Framework;

namespace Tests.Behaviours
{
	// Unit tests for the deterministic clock. These exercise the clock in isolation (no game loop), which is
	// where the Phase-1 determinism guarantee actually lives: the clock advances by a constant step with no
	// wall-clock input, so two runs ticked identically produce an identical time sequence. A full end-to-end
	// game-replay test needs PlayMode (behaviour Update doesn't run in EditMode) and a seeded PRNG (RandomMath
	// is still on the global RNG), both explicitly deferred past Phase 1 — see the Determinism section of
	// Assembler/CLAUDE.md.
	public class FixedStepGameClockTests
	{
		private const float Step = 1f / 60f;

		[Test]
		public void Tick_AdvancesByConstantStep_IgnoringWallClock()
		{
			var clock = new FixedStepGameClock(Step);

			clock.Tick();

			Assert.AreEqual(Step, clock.DeltaTime, 1e-6f);
			Assert.AreEqual(Step, clock.UnscaledDeltaTime, 1e-6f);
			Assert.AreEqual(Step, clock.Time, 1e-6f);
			Assert.AreEqual(1, clock.FrameCount);
		}

		[Test]
		public void Tick_AccumulatesTimeAndFrames()
		{
			var clock = new FixedStepGameClock(Step);

			for (var i = 0; i < 120; i++)
			{
				clock.Tick();
			}

			Assert.AreEqual(120, clock.FrameCount);
			Assert.AreEqual(120 * Step, clock.Time, 1e-4f);
		}

		[Test]
		public void DefaultStep_Is60Fps()
		{
			var clock = new FixedStepGameClock();

			Assert.AreEqual(FixedStepGameClock.DefaultStepSeconds, clock.StepSeconds);
			Assert.AreEqual(1f / 60f, clock.StepSeconds, 1e-6f);
		}

		[Test]
		public void NonPositiveStep_FallsBackToDefault()
		{
			Assert.AreEqual(FixedStepGameClock.DefaultStepSeconds, new FixedStepGameClock(0f).StepSeconds);
			Assert.AreEqual(FixedStepGameClock.DefaultStepSeconds, new FixedStepGameClock(-1f).StepSeconds);
		}

		[Test]
		public void TimeScale_ScalesDeltaButNotUnscaled()
		{
			var clock = new FixedStepGameClock(Step) { TimeScale = 0.5f };

			clock.Tick();

			Assert.AreEqual(Step * 0.5f, clock.DeltaTime, 1e-6f);
			Assert.AreEqual(Step, clock.UnscaledDeltaTime, 1e-6f);
			Assert.AreEqual(Step * 0.5f, clock.Time, 1e-6f);
		}

		[Test]
		public void TimeScale_IsClampedNonNegative()
		{
			var clock = new FixedStepGameClock(Step) { TimeScale = -2f };

			Assert.AreEqual(0f, clock.TimeScale);
		}

		[Test]
		public void Pause_FreezesDeltaAndFrameCount()
		{
			var clock = new FixedStepGameClock(Step);
			clock.Tick();

			clock.Pause();
			clock.Tick();

			Assert.IsTrue(clock.IsPaused);
			Assert.AreEqual(0f, clock.DeltaTime);
			Assert.AreEqual(1, clock.FrameCount);
			Assert.AreEqual(Step, clock.Time, 1e-6f);
		}

		[Test]
		public void UnscaledDelta_StaysConstantWhilePaused()
		{
			var clock = new FixedStepGameClock(Step);
			clock.Pause();

			clock.Tick();

			// The fixed step is a property of the clock, not of pause state; only the scaled delta zeroes out.
			Assert.AreEqual(Step, clock.UnscaledDeltaTime, 1e-6f);
			Assert.AreEqual(0f, clock.DeltaTime);
		}

		[Test]
		public void Step_AdvancesExactlyOneFrameWhilePaused()
		{
			var clock = new FixedStepGameClock(Step);
			clock.Pause();

			clock.Step();
			clock.Tick(); // consumes the one queued step
			clock.Tick(); // frozen again

			Assert.AreEqual(1, clock.FrameCount);
			Assert.AreEqual(Step, clock.Time, 1e-6f);
		}

		[Test]
		public void Step_WhileRunning_IsIgnored()
		{
			var clock = new FixedStepGameClock(Step);

			clock.Step(5);
			clock.Tick();

			// Not paused, so the step queue never applies; this is just a normal tick.
			Assert.AreEqual(1, clock.FrameCount);
		}

		[Test]
		public void Resume_RestoresAdvancement()
		{
			var clock = new FixedStepGameClock(Step);
			clock.Pause();
			clock.Tick();

			clock.Resume();
			clock.Tick();

			Assert.IsFalse(clock.IsPaused);
			Assert.AreEqual(1, clock.FrameCount);
			Assert.AreEqual(Step, clock.Time, 1e-6f);
		}

		[Test]
		public void CaptureDeltaTime_EqualsStep()
		{
			Assert.AreEqual(Step, new FixedStepGameClock(Step).CaptureDeltaTime, 1e-6f);
		}

		// The core determinism property: two independent clocks driven by the same script of operations produce
		// bit-identical time sequences, because nothing about a fixed-step tick depends on wall-clock or any
		// external state. RealtimeGameClock cannot make this guarantee (its Tick reads UnityEngine.Time.deltaTime).
		[Test]
		public void TwoRuns_ProduceIdenticalSequences()
		{
			var a = SimulateRun();
			var b = SimulateRun();

			CollectionAssert.AreEqual(a, b);
		}

		private static float[] SimulateRun()
		{
			var clock = new FixedStepGameClock(Step);
			var samples = new List<float>();

			for (var i = 0; i < 200; i++)
			{
				// A deterministic mix of the operations a real run performs, to exercise more than plain ticks.
				if (i == 50)
				{
					clock.TimeScale = 0.25f;
				}

				if (i == 100)
				{
					clock.Pause();
					clock.Step();
				}

				if (i == 150)
				{
					clock.Resume();
					clock.TimeScale = 1f;
				}

				clock.Tick();
				samples.Add((float)clock.Time);
			}

			return samples.ToArray();
		}
	}
}
