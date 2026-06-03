using System;
using System.Collections.Generic;
using System.Reflection;
using Assembler.Parsing.Info;

namespace Assembler.Parsing
{
	/// <summary>
	/// Accumulates the anonymous <see cref="ExpressionInfo"/>s synthesised for inline
	/// <c>!expr { Do: '&lt;C# body&gt;' }</c> call sites. Shared by reference across every
	/// <see cref="TransformContext"/> sibling produced by <see cref="TransformContext.WithParameters"/>
	/// so the generated ids (<c>__inline_0</c>, <c>__inline_1</c>, …) stay unique and stable across
	/// parameter scopes, and so <see cref="Transformer.Transform"/> can collect them all and hand
	/// them to the compiler alongside the declared expressions.
	/// </summary>
	public sealed class InlineExpressionAccumulator
	{
		private readonly List<ExpressionInfo> _expressions = new();

		public IReadOnlyList<ExpressionInfo> Expressions => _expressions;

		public int Count => _expressions.Count;

		/// <summary>Records a synthesised inline expression, returning the stable id assigned to it.</summary>
		public string Add(Func<string, ExpressionInfo> build)
		{
			var id = $"__inline_{_expressions.Count}";
			_expressions.Add(build(id));
			return id;
		}
	}

	/// <summary>
	/// Per-parse-call context carrying the data the parse pipeline needs but doesn't fit naturally
	/// on the immutable <see cref="ValueSource{T}"/> records: the resolved variable/constant table,
	/// the current parameter scope for template substitution, the expression-by-id lookup so
	/// <c>!expr</c> arguments can be built strongly typed, and the type-name registry used to
	/// resolve those argument types. Also owns a <see cref="MakeGenericMethod"/> cache and the
	/// inline-expression accumulator, both scoped to the lifetime of one
	/// <see cref="Transformer.Transform"/> call.
	/// </summary>
	public sealed class TransformContext
	{
		public IReadOnlyList<ValueInfo> Values { get; }
		public IReadOnlyDictionary<string, AssemblerValue> Parameters { get; }
		public IReadOnlyDictionary<string, ExpressionInfo> ExpressionsById { get; }
		public IReadOnlyDictionary<string, Type> TypeRegistry { get; }
		public Dictionary<Type, MethodInfo> ExprArgFactoryCache { get; }
		public InlineExpressionAccumulator InlineExpressions { get; }

		public TransformContext(
			IReadOnlyList<ValueInfo> values,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyDictionary<string, ExpressionInfo> expressionsById,
			IReadOnlyDictionary<string, Type> typeRegistry)
			: this(values, parameters, expressionsById, typeRegistry, new Dictionary<Type, MethodInfo>(),
				new InlineExpressionAccumulator())
		{
		}

		private TransformContext(
			IReadOnlyList<ValueInfo> values,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyDictionary<string, ExpressionInfo> expressionsById,
			IReadOnlyDictionary<string, Type> typeRegistry,
			Dictionary<Type, MethodInfo> exprArgFactoryCache,
			InlineExpressionAccumulator inlineExpressions)
		{
			Values = values;
			Parameters = parameters;
			ExpressionsById = expressionsById;
			TypeRegistry = typeRegistry;
			ExprArgFactoryCache = exprArgFactoryCache;
			InlineExpressions = inlineExpressions;
		}

		/// <summary>
		/// Returns a sibling context with a new parameter scope, sharing the stable
		/// values/expressions/type-registry/factory-cache/inline-accumulator with this one.
		/// </summary>
		public TransformContext WithParameters(IReadOnlyDictionary<string, AssemblerValue> parameters) =>
			new(Values, parameters, ExpressionsById, TypeRegistry, ExprArgFactoryCache, InlineExpressions);
	}
}
