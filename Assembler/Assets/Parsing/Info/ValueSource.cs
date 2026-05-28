using System.Collections.Generic;
using System.Linq;

namespace Assembler.Parsing.Info
{

	public interface IValueSourceArg
	{
		IValueSourceArg SubstituteParameters(TransformContext ctx);

		object Resolve(IValueSourceResolver resolver);
	}

	public interface IValueSourceResolver
	{
		object Resolve<T>(ValueSource<T> source);
	}

	public abstract record ValueSource<T> : IValueSourceArg
	{
		public virtual ValueSource<T> SubstituteParameters(TransformContext ctx) => this;

		IValueSourceArg IValueSourceArg.SubstituteParameters(TransformContext ctx) => SubstituteParameters(ctx);

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
		public override ValueSource<T> SubstituteParameters(TransformContext ctx) =>
			new ExpressionSource<T>(ExpressionId,
				Arguments.Select(a => a.SubstituteParameters(ctx)).ToArray());
	}

	public sealed record ParameterSource<T>(string ParameterId) : ValueSource<T>
	{
		public override ValueSource<T> SubstituteParameters(TransformContext ctx) =>
			ctx.Parameters.TryGetValue(ParameterId, out var raw)
				? Transformer.CreateValueSource<T>(ctx, raw)
				: throw new ParsingException($"Parameter '{ParameterId}' not supplied during template instantiation");
	}

	public sealed record AssetSource<T>(string AssetId) : ValueSource<T>;

	public sealed record EntityPositionSource<T>(string EntityId) : ValueSource<T>;

	public sealed record TriggerOutputSource<T>(string OutputName) : ValueSource<T>;
}
