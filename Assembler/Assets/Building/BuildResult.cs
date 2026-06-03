using Assembler.Resolving;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Building
{
	/// <summary>
	/// The handles a built game exposes to its caller: the scene root, the live registries, the game clock, and the
	/// seed the run was started with. Lets tooling and tests inspect or tear down a running game (e.g. read variables,
	/// drive frames, capture the seed for a replay header). See the Determinism (Level 1) section in CLAUDE.md.
	/// </summary>
	public sealed record BuildResult(
		GameObject Root,
		BehaviourRegistry BehaviourRegistry,
		VariableRegistry VariableRegistry,
		IGameClock Clock,
		uint Seed);
}
