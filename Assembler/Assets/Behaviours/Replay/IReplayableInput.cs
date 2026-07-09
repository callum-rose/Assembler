using Assembler.Parsing.Info;
using Assembler.Resolving;

namespace Assembler.Behaviours.Replay
{
	/// <summary>
	/// The non-generic surface the replay player uses to re-fire an input trigger without knowing its
	/// <c>TData</c>. Implemented by <see cref="Triggers.Input.InputTrigger{T}"/>; the run's builder registers every
	/// input trigger with the active <see cref="InputReplaySession"/> through this interface so a recorded emission
	/// can be routed back to the exact trigger that produced it.
	/// </summary>
	public interface IReplayableInput
	{
		/// <summary>This trigger's stable key — <c>(entity id, behaviour id)</c> — matching the descriptor recorded in the log.</summary>
		BehaviourDescriptor Descriptor { get; }

		/// <summary>Re-emit a recorded context straight to this trigger's listeners (bypassing capture, so replay never re-records).</summary>
		void ReplayEmit(TriggerContext ctx);
	}
}
