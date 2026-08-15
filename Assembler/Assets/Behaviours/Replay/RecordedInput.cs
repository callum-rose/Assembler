using Assembler.Parsing.Info;
using Assembler.Resolving;

namespace Assembler.Behaviours.Replay
{
	/// <summary>
	/// One captured input emission: the deterministic tick it fired on (<see cref="Assembler.Time.IGameClock.FrameCount"/>),
	/// the input trigger that fired it (keyed by <see cref="BehaviourDescriptor"/>), and the <see cref="TriggerContext"/>
	/// it carried. The ordered sequence of these is the replay log — replayed by re-emitting each context on its
	/// <see cref="Frame"/> into the same trigger's listeners.
	/// </summary>
	public sealed record RecordedInput(int Frame, BehaviourDescriptor Trigger, TriggerContext Context);
}
