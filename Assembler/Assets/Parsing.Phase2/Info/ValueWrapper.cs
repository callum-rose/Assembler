using System.Collections.Generic;

namespace Assembler.Parsing.Phase2.Parsing.Phase2.Info
{
	public abstract record ValueWrapper<T>;

	public sealed record Constant<T>(T Value) : ValueWrapper<T>;

	public sealed record VariableRef<T>(string VariableId) : ValueWrapper<T>;

	public sealed record ExpressionRef<T>(
		string ExpressionId,
		IReadOnlyList<ValueWrapper<object>> Arguments) : ValueWrapper<T>;
}
