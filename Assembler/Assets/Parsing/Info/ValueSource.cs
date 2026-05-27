using System.Collections.Generic;
using System.Linq;

namespace Assembler.Parsing.Info
{

	public interface IValueSourceArg
	{
		IValueSourceArg SubstituteParameters(
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues);

		object Resolve(IValueSourceResolver resolver);
	}

	public interface IValueSourceResolver
	{
		object Resolve<T>(ValueSource<T> source);
	}

	public abstract record ValueSource<T> : IValueSourceArg
	{
		public virtual ValueSource<T> SubstituteParameters(
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) => this;

		IValueSourceArg IValueSourceArg.SubstituteParameters(
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) => SubstituteParameters(parameters, allValues);

		public object Resolve(IValueSourceResolver resolver) => resolver.Resolve(this);
	}

	public sealed record None<T> : ValueSource<T>
	{
		public readonly static None<T> Instance = new();
	}

	public sealed record ConstantSource<T>(T Value) : ValueSource<T>;

	public sealed record ValueReferenceSource<T>(string VariableId) : ValueSource<T>;

	public sealed record ExpressionSource<T>(
		string ExpressionId,
		IReadOnlyList<IValueSourceArg> Arguments) : ValueSource<T>
	{
		public override ValueSource<T> SubstituteParameters(
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new ExpressionSource<T>(ExpressionId,
				Arguments.Select(a => a.SubstituteParameters(parameters, allValues)).ToArray());
	}

	public sealed record ParameterSource<T>(string ParameterId) : ValueSource<T>
	{
		public override ValueSource<T> SubstituteParameters(
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			parameters.TryGetValue(ParameterId, out var raw)
				? Transformer.CreateValueSource<T>(allValues, raw, parameters: parameters)
				: throw new ParsingException($"Parameter '{ParameterId}' not supplied during template instantiation");
	}

	public sealed record AssetSource<T>(string AssetId) : ValueSource<T>;

	public sealed record EntityPositionSource<T>(string EntityId) : ValueSource<T>;

	public sealed record TriggerOutputSource<T>(string OutputName) : ValueSource<T>;
}