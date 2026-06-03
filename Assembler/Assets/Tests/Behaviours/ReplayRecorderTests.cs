using Assembler.Building.Replay;
using Assembler.Input;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using Assembler.Time;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	/// <summary>
	/// Verifies the recorder buckets activations by the clock's FrameCount (the logical tick) and preserves their
	/// order within a tick, capturing the emitted payload. See the Determinism (Level 1) section in CLAUDE.md.
	/// </summary>
	public class ReplayRecorderTests
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

		private static ReplayRecorder NewRecorder(FakeClock clock) =>
			NewRecorderWith(clock, "hash", 7u, 1f / 60f, InputPlatform.Desktop);

		private static ReplayRecorder NewRecorderWith(FakeClock clock, string hash, uint seed, float dt, InputPlatform platform)
		{
			var recorder = new ReplayRecorder();
			recorder.Initialise(clock, hash, seed, dt, platform);
			return recorder;
		}

		[Test]
		public void BucketsActivationsBySharedTick()
		{
			var clock = new FakeClock { FrameCount = 0 };
			var recorder = NewRecorder(clock);

			recorder.Record(new BehaviourDescriptor("a", "t"), TriggerContext.Empty);
			recorder.Record(new BehaviourDescriptor("b", "t"), TriggerContext.Empty);

			clock.FrameCount = 1; // next tick, no firings

			clock.FrameCount = 2;
			recorder.Record(new BehaviourDescriptor("c", "t"), TriggerContext.Empty);

			var replay = recorder.Build();

			Assert.AreEqual(2, replay.Frames.Count, "ticks with no firings produce no frame");
			Assert.AreEqual(0, replay.Frames[0].Tick);
			Assert.AreEqual(2, replay.Frames[0].Activations.Count);
			Assert.AreEqual(new BehaviourDescriptor("a", "t"), replay.Frames[0].Activations[0].Trigger);
			Assert.AreEqual(new BehaviourDescriptor("b", "t"), replay.Frames[0].Activations[1].Trigger);
			Assert.AreEqual(2, replay.Frames[1].Tick);
			Assert.AreEqual(new BehaviourDescriptor("c", "t"), replay.Frames[1].Activations[0].Trigger);
		}

		[Test]
		public void CapturesPayloadSortedByKey()
		{
			var clock = new FakeClock { FrameCount = 3 };
			var recorder = NewRecorder(clock);

			var ctx = TriggerContext.Empty
				.With("y", 2f)
				.With("x", 1f)
				.With("axis", new Vector2(1f, 2f));

			recorder.Record(new BehaviourDescriptor("e", "axis"), ctx);

			var payload = recorder.Build().Frames[0].Activations[0].Payload;

			Assert.AreEqual(3, payload.Count);
			Assert.AreEqual("axis", payload[0].Key, "keys are sorted ordinally for a canonical recording");
			Assert.AreEqual("x", payload[1].Key);
			Assert.AreEqual("y", payload[2].Key);
			Assert.AreEqual(new Vector2(1f, 2f), payload[0].Value);
		}

		[Test]
		public void BuildCarriesHeader()
		{
			var clock = new FakeClock();
			var recorder = NewRecorderWith(clock, "abc123", 99u, 1f / 50f, InputPlatform.Mobile);

			var replay = recorder.Build();

			Assert.AreEqual("abc123", replay.DescriptorHash);
			Assert.AreEqual(99u, replay.Seed);
			Assert.AreEqual(1f / 50f, replay.FixedDeltaTime);
			Assert.AreEqual(InputPlatform.Mobile, replay.Platform);
		}
	}
}
