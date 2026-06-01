using Assembler.Behaviours.Spawners;
using Assembler.Parsing.Controls;
using Assembler.Resolving;
using UnityEngine.InputSystem;

namespace Assembler.Building
{
	public sealed record BehaviourBuildContext(
		ResolutionContext Resolution,
		IEntitySpawner Spawner,
		ExclusiveGroupRegistry ExclusiveGroups,
		ControlsInfo Controls,
		InputActionAsset ControlsAsset);
}
