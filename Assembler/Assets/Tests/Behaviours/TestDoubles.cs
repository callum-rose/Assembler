using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Resolving;
using Assembler.Time;
using UnityEngine;

namespace Tests.Behaviours
{
	/// <summary>
	/// Hand-driven <see cref="IGameClock"/> for unit tests: every property is settable, and
	/// <see cref="Advance"/> simulates a frame tick (accumulating <see cref="Time"/> and
	/// <see cref="FrameCount"/>). <c>Tick()</c> is not on the interface, so a fake need not implement it.
	/// </summary>
	public sealed class FakeGameClock : IGameClock
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

	/// <summary>
	/// A <see cref="Listener"/> that forwards each prepared <see cref="TriggerContext"/> to a delegate —
	/// the standard test spy for asserting what a trigger forwards downstream.
	/// </summary>
	public sealed class ActionListener : Listener
	{
		private readonly Action<TriggerContext> _action;

		public ActionListener(Action<TriggerContext> action)
			: base(new Dictionary<string, string>())
		{
			_action = action;
		}

		public override void Notify(TriggerContext ctx) => _action(Prepare(ctx));

#if DEBUG_CONSOLE
		public override IEnumerable<GameBehaviour> DebugTargets() => Enumerable.Empty<GameBehaviour>();
#endif
	}
}
