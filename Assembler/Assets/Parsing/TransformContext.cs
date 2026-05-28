using System;
using System.Collections.Generic;
using System.Reflection;
using Assembler.Parsing.Info;

namespace Assembler.Parsing
{
	/// <summary>
	/// Per-parse-call context carrying the data the parse pipeline needs but doesn't fit naturally
	/// on the immutable <see cref="ValueSource{T}"/> records: the resolved variable/constant table,
	/// the current parameter scope for template substitution, the expression-by-id lookup so
	/// <c>!expr</c> arguments can be built strongly typed, and the type-name registry used to
	/// resolve those argument types. Also owns a <see cref="MakeGenericMethod"/> cache scoped to
	/// the lifetime of one <see cref="Transformer.Transform"/> call.
	/// </summary>
	public sealed class TransformContext
	{
		public IReadOnlyList<ValueInfo> Values { get; }
		public IReadOnlyDictionary<string, AssemblerValue> Parameters { get; }
		public IReadOnlyDictionary<string, ExpressionInfo> ExpressionsById { get; }
		public IReadOnlyDictionary<string, Type> TypeRegistry { get; }
		public Dictionary<Type, MethodInfo> ExprArgFactoryCache { get; }

		public TransformContext(
			IReadOnlyList<ValueInfo> values,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyDictionary<string, ExpressionInfo> expressionsById,
			IReadOnlyDictionary<string, Type> typeRegistry)
			: this(values, parameters, expressionsById, typeRegistry, new Dictionary<Type, MethodInfo>())
		{
		}

		private TransformContext(
			IReadOnlyList<ValueInfo> values,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyDictionary<string, ExpressionInfo> expressionsById,
			IReadOnlyDictionary<string, Type> typeRegistry,
			Dictionary<Type, MethodInfo> exprArgFactoryCache)
		{
			Values = values;
			Parameters = parameters;
			ExpressionsById = expressionsById;
			TypeRegistry = typeRegistry;
			ExprArgFactoryCache = exprArgFactoryCache;
		}

		/// <summary>
		/// Returns a sibling context with a new parameter scope, sharing the stable
		/// values/expressions/type-registry/factory-cache with this one.
		/// </summary>
		public TransformContext WithParameters(IReadOnlyDictionary<string, AssemblerValue> parameters) =>
			new(Values, parameters, ExpressionsById, TypeRegistry, ExprArgFactoryCache);
	}
}
