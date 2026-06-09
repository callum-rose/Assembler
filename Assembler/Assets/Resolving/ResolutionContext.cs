using Assembler.Time;

namespace Assembler.Resolving
{
	public sealed record ResolutionContext(
		VariableRegistry Variables,
		CompiledExpressionsRegistry Expressions,
		AssetRegistry Assets,
		StringTableRegistry Strings,
		EntityVariableScope Scope,
		EntityTransformRegistry EntityTransforms,
		EntityQueryService EntityQuery,
		IGameClock Clock);
}
