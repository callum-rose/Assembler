using System.Collections.Generic;

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
			IReadOnlyList<ValueInfo> allValues)
		{
			var substituted = new IValueSourceArg[Arguments.Count];
			for (int i = 0; i < Arguments.Count; i++)
			{
				substituted[i] = Arguments[i].SubstituteParameters(parameters, allValues);
			}
			return new ExpressionSource<T>(ExpressionId, substituted);
		}
	}

	public sealed record ParameterSource<T>(string ParameterId) : ValueSource<T>
	{
		public override ValueSource<T> SubstituteParameters(
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues)
		{
			if (!parameters.TryGetValue(ParameterId, out var raw))
			{
				throw new ParsingException(
					$"Parameter '{ParameterId}' not supplied during template instantiation");
			}
			return Transformer.CreateValueSource<T>(allValues, raw, parameters: parameters);
		}
	}

	public sealed record AssetSource<T>(string AssetId) : ValueSource<T>;

	public sealed record EntityPositionSource<T>(string EntityId) : ValueSource<T>;

	public sealed record TriggerOutputSource<T>(string OutputName) : ValueSource<T>;
}
