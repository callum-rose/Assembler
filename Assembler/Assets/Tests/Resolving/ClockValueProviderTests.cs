using Assembler.Parsing.Info;
using Assembler.Resolving;
using Assembler.Time;
using NUnit.Framework;

namespace Tests.Resolving
{
	public class ClockValueProviderTests
	{
		private sealed class FakeGameClock : IGameClock
		{
			public float DeltaTime { get; set; }
			public float UnscaledDeltaTime { get; set; }
			public double Time { get; set; }
			public int FrameCount { get; set; }
			public float TimeScale { get; set; } = 1f;
			public bool IsPaused { get; set; }
			public void Pause() => IsPaused = true;
			public void Resume() => IsPaused = false;
			public void Step(int frames = 1) { }
		}

		private static ResolutionContext ContextWith(IGameClock clock) =>
			// Only the clock is exercised when resolving a ClockValueSource; the other registries are
			// not touched, so they can be left null for this focused test.
			new(null!, null!, null!, null, null!, clock);

		[Test]
		public void ResolvesDeltaTimeFromInjectedClock()
		{
			var fake = new FakeGameClock { DeltaTime = 0.25f };
			var provider = new ClockValueSource<float>(ClockProperty.DeltaTime).Resolve(ContextWith(fake));

			Assert.AreEqual(0.25f, provider.Get(TriggerContext.Empty));

			// Live, not snapshotted: a later read sees the updated clock value.
			fake.DeltaTime = 0.5f;
			Assert.AreEqual(0.5f, provider.Get(TriggerContext.Empty));
		}

		[Test]
		public void ResolvesFrameCountAsInt()
		{
			var fake = new FakeGameClock { FrameCount = 7 };
			var provider = new ClockValueSource<int>(ClockProperty.FrameCount).Resolve(ContextWith(fake));

			Assert.AreEqual(7, provider.Get(TriggerContext.Empty));
		}

		[Test]
		public void ResolvesTimeAsDouble()
		{
			var fake = new FakeGameClock { Time = 3.5d };
			var provider = new ClockValueSource<double>(ClockProperty.Time).Resolve(ContextWith(fake));

			Assert.AreEqual(3.5d, provider.Get(TriggerContext.Empty));
		}

		[Test]
		public void ResolvesUnscaledDeltaTime()
		{
			var fake = new FakeGameClock { UnscaledDeltaTime = 0.016f };
			var provider = new ClockValueSource<float>(ClockProperty.UnscaledDeltaTime).Resolve(ContextWith(fake));

			Assert.AreEqual(0.016f, provider.Get(TriggerContext.Empty));
		}
	}
}
