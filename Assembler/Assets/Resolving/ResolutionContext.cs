namespace Assembler.Resolving
{
	public sealed record ResolutionContext(
		VariableRegistry Variables,
		CompiledExpressionsRegistry Expressions,
		AssetRegistry Assets,
		EntityVariableScope? Scope,
		EntityTransformRegistry EntityTransforms);
}
