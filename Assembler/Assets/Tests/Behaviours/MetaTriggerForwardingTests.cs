using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Behaviours.Gating;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	/// <summary>
	/// Locks in the headline behaviour of the trigger-context refactor: a meta trigger (debounce, throttle,
	/// condition gate, exclusive) that wraps a data-producing upstream must forward the upstream's outputs
	/// to its downstream listeners without losing them.
	/// </summary>
	public class MetaTriggerForwardingTests
	{
		private sealed class CapturingListener : Listener
		{
			public TriggerContext? Last { get; private set; }
			public int CallCount { get; private set; }

			public CapturingListener() : base(new Dictionary<string, string>()) { }

			public override void Notify(TriggerContext ctx)
			{
				Last = Prepare(ctx);
				CallCount++;
			}

#if DEBUG_CONSOLE
			public override IEnumerable<GameBehaviour> DebugTargets() => Enumerable.Empty<GameBehaviour>();
#endif
		}

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

		[Test]
		public void DebouncedTrigger_ForwardsUpstreamOutputs_ToDownstreamListener()
		{
			var go = new GameObject("DebouncedForwardingTest");
			try
			{
				var trigger = go.AddComponent<DebouncedTrigger>();
				trigger.Clock = new FakeGameClock();
				var listener = new CapturingListener();

				// Zero interval — every fire forwards.
				trigger.Initialise(
					new DebouncedTriggerData("debounce", new ValueProvider<float>(0f)),
					new List<Listener> { listener });

				var upstreamCtx = TriggerContext.Empty
					.With("contact_point", new Vector3(1, 2, 3))
					.With("other_velocity", new Vector3(4, 5, 6));

				trigger.Execute(upstreamCtx);

				Assert.AreEqual(1, listener.CallCount);
				Assert.IsNotNull(listener.Last);
				Assert.AreEqual(new Vector3(1, 2, 3), listener.Last!.Get<Vector3>("contact_point"));
				Assert.AreEqual(new Vector3(4, 5, 6), listener.Last!.Get<Vector3>("other_velocity"));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ConditionGate_ForwardsUpstreamOutputs_WhenConditionTrue()
		{
			var go = new GameObject("ConditionGateForwardingTest");
			try
			{
				var trigger = go.AddComponent<ConditionGate>();
				var listener = new CapturingListener();

				trigger.Initialise(
					new ConditionGateData("gate", new ValueProvider<bool>(true)),
					new List<Listener> { listener });

				var upstreamCtx = TriggerContext.New("item", "hello");

				trigger.Execute(upstreamCtx);

				Assert.AreEqual(1, listener.CallCount);
				Assert.AreEqual("hello", listener.Last!.Get<string>("item"));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ConditionGate_DropsNotification_WhenConditionFalse()
		{
			var go = new GameObject("ConditionGateDropTest");
			try
			{
				var trigger = go.AddComponent<ConditionGate>();
				var listener = new CapturingListener();

				trigger.Initialise(
					new ConditionGateData("gate", new ValueProvider<bool>(false)),
					new List<Listener> { listener });

				trigger.Execute(TriggerContext.New("item", "hello"));

				Assert.AreEqual(0, listener.CallCount);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}
	}
}
