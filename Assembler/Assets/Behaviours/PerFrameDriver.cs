using System;
using System.Collections.Generic;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Behaviours
{
	/// <summary>
	/// Steps every <see cref="IPerFrameStepped"/> behaviour once per advanced game frame, in the registration
	/// (descriptor) order the <c>BehaviourRegistry</c> hands over. Replaces each per-frame behaviour driving itself
	/// off its own <c>Update</c> — Unity leaves the order between different components' <c>Update</c> undefined, so a
	/// shared-velocity stack (acceleration → drag → speed limit → velocity) would compose in an undefined order and
	/// diverge on replay. Central ordered stepping closes that same-tick carve-out (issue #241).
	/// </summary>
	/// <remarks>
	/// <see cref="DefaultExecutionOrderAttribute"/> places this after <c>GameClockDriver</c> (-10000, ticks the
	/// clock) and <c>ReplayDriver</c> (-9000, delivers replayed input), but ahead of everything else — it <em>is</em>
	/// the gameplay tick. The step list is the registry's live list, so entities spawned or destroyed mid-run add to
	/// and drop from it through the registry's register/deregister path without any extra wiring here.
	/// </remarks>
	[DefaultExecutionOrder(-8000)]
	public sealed class PerFrameDriver : MonoBehaviour
	{
		private IReadOnlyList<IPerFrameStepped> _behaviours = Array.Empty<IPerFrameStepped>();
		private IGameClock _clock = null!;
		private int _lastSteppedFrame = -1;

		public void Initialise(IReadOnlyList<IPerFrameStepped> behaviours, IGameClock clock)
		{
			_behaviours = behaviours;
			_clock = clock;
		}

		private void Update()
		{
			// FrameCount only advances on a game frame (frozen while paused, +1 for a queued debug step), so the
			// whole stack steps once per advanced frame and never while paused — a debug step still advances motion.
			if (_clock.FrameCount == _lastSteppedFrame)
			{
				return;
			}

			_lastSteppedFrame = _clock.FrameCount;

			// Iterate by index over the live list: within a frame it only ever grows (a Step that spawns appends;
			// destroys deregister at end-of-frame), so a count snapshot keeps indices stable and defers anything
			// spawned this frame to the next tick. Guard against a behaviour a prior Step destroyed this frame.
			var count = _behaviours.Count;
			for (var i = 0; i < count; i++)
			{
				var behaviour = _behaviours[i];
				if (behaviour is UnityEngine.Object obj && obj == null)
				{
					continue;
				}

				behaviour.Step();
			}
		}
	}
}
