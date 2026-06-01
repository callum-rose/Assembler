
using System;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers.Input
{
	/// <summary>
	/// Base class for triggers that fire from player input (keyboard, mouse, gamepad, touch gestures). These cannot
	/// be executed manually (<see cref="Execute"/> throws); subclasses just call <c>NotifyListeners</c> as usual.
	/// This class overrides <c>NotifyListeners</c> to route every input firing through the record/replay seam, so a
	/// new input trigger cannot accidentally bypass recording — there is nothing extra to remember to call.
	/// See the Determinism (Level 1) section in CLAUDE.md.
	/// </summary>
	public abstract class InputTrigger<T> : Trigger<T>, IReplayableInputTrigger, INeedsInputBoundary where T : TriggerData
	{
		// Injected per build (mirrors INeedsGameClock). Null when a trigger is constructed outside the build
		// pipeline (e.g. unit tests), in which case firing simply behaves as a normal, unrecorded notification.
		private InputBoundary? _boundary;

		InputBoundary INeedsInputBoundary.InputBoundary { set => _boundary = value; }

		public override void Execute(TriggerContext ctx)
		{
			throw new Exception("Cannot execute an input trigger manually");
		}

		/// <summary>
		/// Records the firing (when recording) and suppresses it during replay, then delegates to the base
		/// notification. Replayed activations bypass this via <see cref="ReplayFire"/>.
		/// </summary>
		protected override void NotifyListeners(TriggerContext ctx)
		{
			if (_boundary != null)
			{
				if (_boundary.ReplayActive)
				{
					return;
				}

				_boundary.Sink?.Record(Descriptor, ctx);
			}

			base.NotifyListeners(ctx);
		}

		/// <summary>Re-fires this trigger with a recorded context, bypassing live device polling. Called only during replay.</summary>
		public void ReplayFire(TriggerContext ctx) => base.NotifyListeners(ctx);
	}
}
