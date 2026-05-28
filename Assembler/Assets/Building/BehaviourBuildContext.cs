using Assembler.Behaviours.Spawners;
using Assembler.Resolving;

namespace Assembler.Building
{
	public sealed record BehaviourBuildContext(
		ResolutionContext Resolution,
		IEntitySpawner Spawner,
		ExclusiveGroupRegistry ExclusiveGroups)
	{
		public static implicit operator ResolutionContext(BehaviourBuildContext ctx) => ctx.Resolution;
	}
}
