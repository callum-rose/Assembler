using Assembler.Time;
using NUnit.Framework;

namespace Tests.Behaviours
{
	/// <summary>
	/// Locks in the deterministic timing foundation: <see cref="FixedStepGameClock"/> advances a constant delta
	/// per tick (so N ticks ⇒ Time == N*dt, FrameCount == N) while matching <see cref="RealtimeGameClock"/>'s
	/// pause/step/timescale semantics. See the Determinism (Level 1) section in CLAUDE.md.
	/// </summary>
	public class FixedStepGameClockTests
	{
		private const float Dt = 1f / 60f;

		[Test]
		public void NTicks_AdvanceTimeByNTimesDelta()
		{
			var clock = new FixedStepGameClock(Dt);

			const int n = 120;
			for (var i = 0; i < n; i++) clock.Tick();

			Assert.AreEqual(n, clock.FrameCount);
			Assert.AreEqual(n * (double)Dt, clock.Time, 1e-9);
			Assert.AreEqual(Dt, clock.DeltaTime, 1e-9f);
		}

		[Test]
		public void DeltaIsConstantAndIndependentOfWallClock()
		{
			var clock = new FixedStepGameClock(0.5f);

			clock.Tick();
			Assert.AreEqual(0.5f, clock.DeltaTime, 1e-9f);
			clock.Tick();
			Assert.AreEqual(0.5f, clock.DeltaTime, 1e-9f);
			Assert.AreEqual(1.0, clock.Time, 1e-9);
		}

		[Test]
		public void TimeScale_ScalesDelta()
		{
			var clock = new FixedStepGameClock(Dt) { TimeScale = 0.5f };

			clock.Tick();

			Assert.AreEqual(Dt * 0.5f, clock.DeltaTime, 1e-9f);
			Assert.AreEqual(Dt * 0.5, clock.Time, 1e-9);
		}

		[Test]
		public void TimeScale_ClampsNegative()
		{
			var clock = new FixedStepGameClock(Dt) { TimeScale = -5f };
			Assert.AreEqual(0f, clock.TimeScale);
		}

		[Test]
		public void UnpausedTickAdvancesFrameCount()
		{
			var clock = new FixedStepGameClock(Dt);
			Assert.AreEqual(0, clock.FrameCount);

			clock.Tick();
			clock.Tick();

			Assert.AreEqual(2, clock.FrameCount);
		}

		[Test]
		public void PausedTickYieldsZeroDeltaAndFrozenFrameCount()
		{
			var clock = new FixedStepGameClock(Dt);
			clock.Tick();
			var frameBeforePause = clock.FrameCount;
			var timeBeforePause = clock.Time;

			clock.Pause();
			clock.Tick();
			clock.Tick();

			Assert.AreEqual(0f, clock.DeltaTime);
			Assert.AreEqual(frameBeforePause, clock.FrameCount);
			Assert.AreEqual(timeBeforePause, clock.Time, 1e-9);
			Assert.IsTrue(clock.IsPaused);
		}

		[Test]
		public void StepAdvancesExactlyOneFrameWhilePaused()
		{
			var clock = new FixedStepGameClock(Dt);
			clock.Pause();
			clock.Tick();
			var frozen = clock.FrameCount;

			clock.Step();
			clock.Tick(); // consumes the queued step: advances one frame
			clock.Tick(); // no step queued: frozen again

			Assert.AreEqual(frozen + 1, clock.FrameCount);
			Assert.IsTrue(clock.IsPaused);
		}

		[Test]
		public void StepQueuesMultipleFrames()
		{
			var clock = new FixedStepGameClock(Dt);
			clock.Pause();
			clock.Tick();
			var frozen = clock.FrameCount;

			clock.Step(3);
			clock.Tick();
			clock.Tick();
			clock.Tick();
			clock.Tick(); // fourth tick: queue empty, frozen

			Assert.AreEqual(frozen + 3, clock.FrameCount);
		}

		[Test]
		public void StepIgnoredWhenNotPaused()
		{
			var clock = new FixedStepGameClock(Dt);
			clock.Step(5); // queued but never consumed while running

			clock.Tick();
			Assert.AreEqual(1, clock.FrameCount);

			// The queued steps must not leak into a later pause.
			clock.Pause();
			clock.Tick();
			Assert.AreEqual(1, clock.FrameCount);
		}

		[Test]
		public void ResumeRestoresAdvance()
		{
			var clock = new FixedStepGameClock(Dt);
			clock.Pause();
			clock.Tick();
			var frozen = clock.FrameCount;

			clock.Resume();
			clock.Tick();

			Assert.AreEqual(frozen + 1, clock.FrameCount);
			Assert.IsFalse(clock.IsPaused);
		}
	}
}
