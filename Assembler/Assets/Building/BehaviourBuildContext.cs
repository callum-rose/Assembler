using Assembler.Behaviours.Spawners;
using Assembler.Behaviours.UI;
using Assembler.Parsing.Controls;
using Assembler.Resolving;
using Assembler.Time;
using UnityEngine.InputSystem;

namespace Assembler.Building
{
	public sealed record BehaviourBuildContext(
		ResolutionContext Resolution,
		IEntitySpawner Spawner,
		ExclusiveGroupRegistry ExclusiveGroups,
		ControlsInfo Controls,
		InputActionAsset ControlsAsset,
		IGameClock Clock,
		UiPrefabLibrary UiPrefabs);
}
