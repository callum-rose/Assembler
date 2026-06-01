using Assembler.Behaviours.Spawners;
using Assembler.Resolving;
using Assembler.Time;

namespace Assembler.Building
{
	public sealed record BehaviourBuildContext(
		ResolutionContext Resolution,
		IEntitySpawner Spawner,
		ExclusiveGroupRegistry ExclusiveGroups,
		IGameClock Clock);
}
