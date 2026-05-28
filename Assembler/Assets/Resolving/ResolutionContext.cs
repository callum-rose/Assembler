namespace Assembler.Resolving
{
	public sealed record ResolutionContext(
		VariableRegistry Variables,
		CompiledExpressionsRegistry Expressions,
		AssetRegistry Assets,
		TriggerContext TriggerContext,
		EntityVariableScope? Scope,
		EntityTransformRegistry EntityTransforms);
}
