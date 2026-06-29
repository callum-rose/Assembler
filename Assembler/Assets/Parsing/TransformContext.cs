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
		public RecordSchemaRegistry RecordSchemas { get; }

		/// <summary>
		/// The concrete id of the entity currently being instantiated, or <c>null</c> outside a self-resolving
		/// pass. Set only on the context used to substitute an entity's own behaviours (in
		/// <see cref="TemplateInstantiator.Instantiate"/>), so an omitted-<c>Id</c> <c>!entity</c>
		/// (a <see cref="Info.SelfEntityId"/>) binds to that id. Deliberately left <c>null</c> when
		/// substituting child behaviours, so a child's self reference stays pending until the child's own
		/// instantiation rather than capturing the parent's id.
		/// </summary>
		public string? EnclosingEntityId { get; }

		public TransformContext(
			IReadOnlyList<ValueInfo> values,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyDictionary<string, ExpressionInfo> expressionsById,
			IReadOnlyDictionary<string, Type> typeRegistry,
			Dictionary<Type, MethodInfo> exprArgFactoryCache,
			InlineExpressionAccumulator inlineExpressions,
			RecordSchemaRegistry recordSchemas,
			string? enclosingEntityId = null)
		{
			Values = values;
			Parameters = parameters;
			ExpressionsById = expressionsById;
			TypeRegistry = typeRegistry;
			ExprArgFactoryCache = exprArgFactoryCache;
			InlineExpressions = inlineExpressions;
			RecordSchemas = recordSchemas;
			EnclosingEntityId = enclosingEntityId;
		}

		/// <summary>
		/// Returns a sibling context with a new parameter scope, sharing the stable
		/// values/expressions/type-registry/factory-cache/inline-accumulator/record-schemas with this one.
		/// The self-resolving <see cref="EnclosingEntityId"/> is intentionally not carried across a parameter
		/// scope change — it is set explicitly via <see cref="WithEnclosingEntityId"/> at the one point a
		/// behaviour is bound to its owning entity.
		/// </summary>
		public TransformContext WithParameters(IReadOnlyDictionary<string, AssemblerValue> parameters) =>
			new(Values, parameters, ExpressionsById, TypeRegistry, ExprArgFactoryCache, InlineExpressions, RecordSchemas);

		/// <summary>
		/// Returns a sibling context that resolves an omitted-<c>Id</c> <c>!entity</c> self reference to
		/// <paramref name="entityId"/>, sharing everything else with this one.
		/// </summary>
		public TransformContext WithEnclosingEntityId(string entityId) =>
			new(Values, Parameters, ExpressionsById, TypeRegistry, ExprArgFactoryCache, InlineExpressions, RecordSchemas,
				entityId);
	}
}
