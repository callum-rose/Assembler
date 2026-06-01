using Assembler.Behaviours.Spawners;
using Assembler.Behaviours.Triggers.Input;
using Assembler.Parsing.Controls;
using Assembler.Resolving;
using UnityEngine.InputSystem;
using Assembler.Time;

namespace Assembler.Building
{
	public sealed record BehaviourBuildContext(
		ResolutionContext Resolution,
		IEntitySpawner Spawner,
		ExclusiveGroupRegistry ExclusiveGroups,
		ControlsInfo Controls,
		InputActionAsset ControlsAsset,
		IGameClock Clock,
		InputBoundary InputBoundary);
}
