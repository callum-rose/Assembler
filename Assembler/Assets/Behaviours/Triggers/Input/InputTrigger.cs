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
	/// This is the record/replay seam (issue #101), made structural rather than convention-based: it <b>shadows</b>
	/// <see cref="GameBehaviour.NotifyListeners"/>, so a subclass just calls <c>NotifyListeners</c> as any trigger
	/// would and automatically gets (a) capture of the emission to the active <see cref="InputReplaySession"/>, and
	/// (b) suppression of the live emission while replaying (the recorded log drives listeners instead). A subclass
	/// that forgets the seam and reaches <c>base.NotifyListeners</c> is the only way to bypass it. Live device
	/// polling in <c>Update</c> may still early-out on <see cref="IsReplaying"/> as an optimisation, but it is no
	/// longer required for correctness — suppression happens at this single choke point.
	/// </remarks>
	public abstract class InputTrigger<T> : Trigger<T>, IReplayableInput where T : TriggerData
	{
		private BehaviourDescriptor? _descriptor;

		/// <summary>This trigger's stable key — <c>(entity id, behaviour id)</c>. Cached (the ids are fixed once the
		/// build's initialisation pass has run), so per-emission recording doesn't allocate a fresh descriptor.</summary>
		public BehaviourDescriptor Descriptor => _descriptor ??= new BehaviourDescriptor(Entity.Id, Id);

		/// <summary>True while the active run is replaying a captured log. Optional early-out for device polling; the
		/// authoritative suppression is in <see cref="NotifyListeners"/>.</summary>
		protected bool IsReplaying => InputReplayHub.Current is { IsReplaying: true };

		/// <summary>
		/// Record-then-notify choke point that shadows <see cref="GameBehaviour.NotifyListeners"/>: captures the
		/// emission to the active session (record mode) and drops it entirely (replay mode), otherwise forwards to
		/// the base. Every input-trigger subclass calls this exactly as it would the base method.
		/// </summary>
		protected new void NotifyListeners(TriggerContext ctx)
		{
			if (InputReplayHub.Current is { } session)
			{
				if (session.IsReplaying)
				{
					return;
				}

				session.Record(Descriptor, ctx);
			}

			base.NotifyListeners(ctx);
		}

		// Replay path: re-emit a recorded context straight to listeners via the base, bypassing capture/suppression.
		void IReplayableInput.ReplayEmit(TriggerContext ctx) => base.NotifyListeners(ctx);
	}
}
