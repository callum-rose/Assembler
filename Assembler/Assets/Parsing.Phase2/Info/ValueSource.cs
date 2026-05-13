using System.Collections.Generic;

namespace Assembler.Parsing.Phase2.Info
{
	public abstract record ValueSource<T>;

	public sealed record None<T> : ValueSource<T>
	{
		public readonly static None<T> Instance = new();
	}

	public sealed record ConstantSource<T>(T Value) : ValueSource<T>;

	public sealed record VariableSource<T>(string VariableId) : ValueSource<T>;

	public sealed record ExpressionSource<T>(
		string ExpressionId,
		IReadOnlyList<ValueSource<object>> Arguments) : ValueSource<T>;
	
	public sealed record ParameterSource<T>(string ParameterId) : ValueSource<T>;
}