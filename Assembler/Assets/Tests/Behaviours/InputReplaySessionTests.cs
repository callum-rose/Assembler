using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours.Replay;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using NUnit.Framework;

namespace Tests.Behaviours
{
	/// <summary>
	/// EditMode unit coverage for the record/replay core (issue #101): the capture log keys emissions by clock
	/// frame + trigger descriptor, and replay re-emits them onto the matching frame, in order, resolving the target
	/// trigger through the bound lookup. The full device-driven end-to-end proof lives in the PlayMode
	/// <c>ReplayDeterminismTests</c>; these tests pin the session logic itself without needing play mode.
	/// </summary>
	public sealed class InputReplaySessionTests
	{
		private static readonly BehaviourDescriptor TriggerA = new("player", "move input");
		private static readonly BehaviourDescriptor TriggerB = new("enemy", "fire input");

		[Test]
		public void Record_tags_each_emission_with_the_current_frame_and_descriptor()
		{
			var clock = new FakeGameClock();
			var session = InputReplaySession.Record();
			session.Bind(clock);

			clock.FrameCount = 2;
			session.Record(TriggerA, Ctx(10));
			clock.FrameCount = 5;
			session.Record(TriggerB, Ctx(20));

			Assert.That(session.Log.Select(r => r.Frame), Is.EqualTo(new[] { 2, 5 }));
			Assert.That(session.Log.Select(r => r.Trigger), Is.EqualTo(new[] { TriggerA, TriggerB }));
			Assert.That(session.Log.Select(r => r.Context.Get<int>("v")), Is.EqualTo(new[] { 10, 20 }));
		}

		[Test]
		public void Record_is_a_no_op_when_the_session_is_replaying()
		{
			var clock = new FakeGameClock();
			var session = InputReplaySession.Replay(new[] { new RecordedInput(0, TriggerA, Ctx(1)) });
			session.Bind(clock);

			// A replay session must never grow its log from live emissions (that would double-fire on replay).
			session.Record(TriggerA, Ctx(99));

			Assert.That(session.Log.Count, Is.EqualTo(1));
			Assert.That(session.Log[0].Context.Get<int>("v"), Is.EqualTo(1));
		}

		[Test]
		public void ReplayFrame_reemits_recorded_contexts_in_order_on_the_matching_frame()
		{
			var log = new[]
			{
				new RecordedInput(1, TriggerA, Ctx(100)),
				new RecordedInput(1, TriggerA, Ctx(101)),
				new RecordedInput(3, TriggerB, Ctx(200)),
			};
			var session = InputReplaySession.Replay(log);
			var a = new FakeInput(TriggerA);
			var b = new FakeInput(TriggerB);
			session.BindTriggerLookup(Lookup(a, b));

			session.ReplayFrame(0);
			Assert.That(a.Received, Is.Empty, "nothing is due on frame 0");

			session.ReplayFrame(1);
			Assert.That(a.Received.Select(c => c.Get<int>("v")), Is.EqualTo(new[] { 100, 101 }),
				"both frame-1 emissions fire, in capture order");
			Assert.That(b.Received, Is.Empty);

			session.ReplayFrame(2);
			Assert.That(b.Received, Is.Empty, "nothing is due on frame 2");

			session.ReplayFrame(3);
			Assert.That(b.Received.Select(c => c.Get<int>("v")), Is.EqualTo(new[] { 200 }));
		}

		[Test]
		public void ReplayFrame_catches_up_emissions_from_any_skipped_frames()
		{
			var log = new[]
			{
				new RecordedInput(1, TriggerA, Ctx(1)),
				new RecordedInput(3, TriggerA, Ctx(3)),
			};
			var session = InputReplaySession.Replay(log);
			var a = new FakeInput(TriggerA);
			session.BindTriggerLookup(Lookup(a));

			// Jumping straight to frame 5 still fires everything due on or before it (the cursor never stalls).
			session.ReplayFrame(5);

			Assert.That(a.Received.Select(c => c.Get<int>("v")), Is.EqualTo(new[] { 1, 3 }));
		}

		[Test]
		public void Bind_resets_the_cursor_so_one_log_replays_against_many_runs()
		{
			var session = InputReplaySession.Replay(new[] { new RecordedInput(1, TriggerA, Ctx(7)) });

			var first = new FakeInput(TriggerA);
			session.BindTriggerLookup(Lookup(first));
			session.Bind(new FakeGameClock());
			session.ReplayFrame(1);
			Assert.That(first.Received.Count, Is.EqualTo(1));

			// A second run of the same session: Bind resets the cursor, so the log drives the new run too rather
			// than the session being silently single-use.
			var second = new FakeInput(TriggerA);
			session.BindTriggerLookup(Lookup(second));
			session.Bind(new FakeGameClock());
			session.ReplayFrame(1);
			Assert.That(second.Received.Count, Is.EqualTo(1), "Bind must reset the replay cursor for a fresh run.");
		}

		private static TriggerContext Ctx(int value) => TriggerContext.New("v", value);

		// Descriptor → trigger lookup over the given fakes, matching the shape the builder binds over BehaviourRegistry.
		private static System.Func<BehaviourDescriptor, IReplayableInput?> Lookup(params FakeInput[] triggers)
		{
			var byDescriptor = triggers.ToDictionary(t => t.Descriptor, t => (IReplayableInput)t);
			return d => byDescriptor.TryGetValue(d, out var t) ? t : null;
		}

		private sealed class FakeInput : IReplayableInput
		{
			public FakeInput(BehaviourDescriptor descriptor) => Descriptor = descriptor;

			public BehaviourDescriptor Descriptor { get; }

			public List<TriggerContext> Received { get; } = new();

			public void ReplayEmit(TriggerContext ctx) => Received.Add(ctx);
		}
	}
}
