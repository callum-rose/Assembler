
using System;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers.Input
{
	/// <summary>
	/// Base class for triggers that fire from player input (keyboard, mouse, gamepad, touch gestures). These cannot
	/// be executed manually (<see cref="Execute"/> throws); subclasses detect their input and call <see cref="FireInput"/>
	/// (never <c>NotifyListeners</c> directly) so the firing routes through the record/replay seam.
	/// </summary>
	public abstract class InputTrigger<T> : Trigger<T>, IReplayableInputTrigger where T : TriggerData
	{
		public override void Execute(TriggerContext ctx)
		{
			throw new Exception("Cannot execute an input trigger manually");
		}

		/// <summary>
		/// Routes a live input firing through the record/replay seam. During replay this is a no-op (the
		/// replay player re-injects recorded activations via <see cref="ReplayFire"/> instead); otherwise
		/// it records the activation (if recording) and notifies listeners. Subclasses call this in place of
		/// <c>NotifyListeners</c>. See the Determinism (Level 1) section in CLAUDE.md.
		/// </summary>
		protected void FireInput(TriggerContext ctx)
		{
			if (InputBoundary.ReplayActive)
			{
				return;
			}

			if (Descriptor is { } descriptor)
			{
				InputBoundary.Sink?.Record(descriptor, ctx);
			}

			NotifyListeners(ctx);
		}

		/// <summary>Re-fires this trigger with a recorded context, bypassing live device polling. Called only during replay.</summary>
		public void ReplayFire(TriggerContext ctx) => NotifyListeners(ctx);
	}
}
