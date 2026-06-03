using System.Collections.Generic;
using Assembler.Input;
using Assembler.Parsing.Info;

namespace Assembler.Building.Replay
{
	/// <summary>
	/// One input trigger firing within a tick: which trigger fired (by stable descriptor) and the exact
	/// <c>TriggerContext</c> payload it emitted, captured as ordered key/value pairs. Replaying this re-fires
	/// the trigger with the same payload, device-independently.
	/// </summary>
	public sealed record InputActivation(BehaviourDescriptor Trigger, IReadOnlyList<KeyValuePair<string, object>> Payload);

	/// <summary>All input activations that fired during a single logical tick, in recorded order.</summary>
	public sealed record InputFrame(int Tick, IReadOnlyList<InputActivation> Activations);

	/// <summary>
	/// A complete recorded session: the descriptor it was captured against (by hash), the run's seed and fixed
	/// delta, the active input platform, and every tick's input activations. Replaying it on the same build/machine
	/// reproduces the run byte-identically. See the Determinism (Level 1) section in CLAUDE.md.
	/// </summary>
	public sealed record Replay(
		string DescriptorHash,
		uint Seed,
		float FixedDeltaTime,
		InputPlatform Platform,
		IReadOnlyList<InputFrame> Frames);
}
