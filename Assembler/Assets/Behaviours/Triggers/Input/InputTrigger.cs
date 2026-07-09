using Assembler.Behaviours.Replay;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers.Input
{
	/// <summary>
	/// Base class for triggers that fire from player input (keyboard, mouse, gamepad, touch gestures). These are
	/// event sources: subclasses notify listeners when their input is detected. They expose no Execute and are not
	/// valid Listeners: targets.
	/// </summary>
	/// <remarks>
	/// This is the record/replay seam (issue #101). Subclasses emit through <see cref="EmitInput"/> (never
	/// <c>NotifyListeners</c> directly), so every input emission is captured to the active
	/// <see cref="InputReplaySession"/>; during replay they must gate their live device reads on
	/// <see cref="IsReplaying"/> so only the recorded log drives the game.
	/// </remarks>
	public abstract class InputTrigger<T> : Trigger<T>, IReplayableInput where T : TriggerData
	{
		/// <summary>This trigger's stable key — <c>(entity id, behaviour id)</c>. Valid once the build's
		/// initialisation pass has assigned the entity and behaviour ids.</summary>
		public BehaviourDescriptor Descriptor => new(Entity.Id, Id);

		/// <summary>True while the active run is replaying a captured log. Subclasses must early-out of their
		/// device-polling code paths on this so the recorded emissions are the only ones that fire.</summary>
		protected bool IsReplaying => InputReplayHub.Current is { Mode: ReplayMode.Replay };

		/// <summary>
		/// Emit an input context to this trigger's listeners, first recording it to the active session when the run
		/// is capturing. Every input-trigger subclass fires through this rather than <c>NotifyListeners</c> so the
		/// emission is captured for replay.
		/// </summary>
		protected void EmitInput(TriggerContext ctx)
		{
			InputReplayHub.Current?.Record(Descriptor, ctx);
			NotifyListeners(ctx);
		}

		// Replay path: re-emit a recorded context straight to listeners, deliberately NOT through EmitInput so a
		// replayed emission is never recorded again.
		void IReplayableInput.ReplayEmit(TriggerContext ctx) => NotifyListeners(ctx);
	}
}
