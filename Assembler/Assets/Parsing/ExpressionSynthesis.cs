using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Parsing.Info;

namespace Assembler.Parsing
{
	// Routes a `!expr { Do, With }` call site to an ExpressionSource — either a named call against a
	// declared expression, or an anonymous inline C# body whose synthesised expression is accumulated on
	// the context. Also infers the operand/return types an inline body needs.
	internal static class ExpressionSynthesis
	{
		/// <summary>
		/// Routes a <c>!expr { Do, With }</c> call site to an <see cref="ExpressionSource{T}"/>.
		/// If <c>Do</c> names a declared expression (by id or <c>CallableAs</c> alias) it's a named
		/// call against that expression's id; otherwise <c>Do</c> is compiled as an anonymous inline
		/// C# body whose params <c>arg0</c>, <c>arg1</c>, … bind positionally to <c>With</c>.
		/// </summary>
		public static ValueSource<T> CreateExpressionSource<T>(TransformContext ctx, ExprRef exprRef)
		{
			if (TryResolveNamedExpression(ctx, exprRef.Do, out var named))
			{
				WarnIfInlineHintsIgnored(exprRef, named.Id);
				return new ExpressionSource<T>(named.Id, BuildNamedExpressionArguments(ctx, exprRef, named));
			}

			return CreateInlineExpressionSource<T>(ctx, exprRef);
		}

		// Resolves a `Do` value to a declared expression by its id first, then by CallableAs alias.
		private static bool TryResolveNamedExpression(TransformContext ctx, string @do, out ExpressionInfo info)
		{
			if (ctx.ExpressionsById.TryGetValue(@do, out info!))
			{
				return true;
			}

			foreach (var candidate in ctx.ExpressionsById.Values)
			{
				if (candidate.CallableAlias == @do)
				{
					info = candidate;
					return true;
				}
			}

			info = null!;
			return false;
		}

		private static IReadOnlyList<IValueSourceArg> BuildNamedExpressionArguments(TransformContext ctx,
			ExprRef exprRef,
			ExpressionInfo info)
		{
			if (info.Arguments.Count != exprRef.With.Count)
			{
				throw new ParsingException(
					$"Expression '{exprRef.Do}' expects {info.Arguments.Count} arguments " +
					$"but {exprRef.With.Count} were supplied.");
			}

			if (exprRef.With.Count == 0)
			{
				return Array.Empty<IValueSourceArg>();
			}

			var args = new IValueSourceArg[exprRef.With.Count];

			for (int i = 0; i < exprRef.With.Count; i++)
			{
				var typeName = info.Arguments[i].type;

				if (!ctx.TypeRegistry.TryGetValue(typeName, out var argType))
				{
					throw new ParsingException(
						$"Expression '{exprRef.Do}' argument {i} has unknown type '{typeName}'.");
				}

				args[i] = ValueSourceFactory.BuildArg(ctx, argType, exprRef.With[i]);
			}

			return args;
		}

		// The inline-only hints (ReturnType / ArgumentTypes / RegisterTypes / RegisterTypeStatics) belong
		// on the declared expression, not the call site, so warn and ignore them when Do names one.
		private static void WarnIfInlineHintsIgnored(ExprRef exprRef, string id)
		{
			if (exprRef.ReturnType != null || exprRef.ArgumentTypes != null
				|| exprRef.RegisterTypes != null || exprRef.RegisterTypeStatics != null)
			{
				UnityEngine.Debug.LogWarning(
					$"!expr 'Do: {exprRef.Do}' names the declared expression '{id}', so ReturnType / " +
					"ArgumentTypes / RegisterTypes / RegisterTypeStatics on the call site are ignored " +
					"(they are declared on the expression itself).");
			}
		}

		// Synthesises an anonymous ExpressionInfo for an inline `Do: '<C# body>'` and accumulates it on
		// the context. Operand (arg0, arg1, …) types are taken from an explicit ArgumentTypes hint when
		// given, otherwise inferred from `With`; the return type is an explicit ReturnType hint when
		// given, otherwise the use-site T (falling back to the first operand). Any RegisterTypes /
		// RegisterTypeStatics hints flow onto the synthesised expression so the body can use them. The
		// body itself is handed verbatim to the compiler, which binds argN to the synthesised params and
		// resolves any other identifier (method, `new`, registered expression) as in any declared body.
		private static ValueSource<T> CreateInlineExpressionSource<T>(TransformContext ctx, ExprRef exprRef)
		{
			var explicitArgTypes = exprRef.ArgumentTypes;

			if (explicitArgTypes != null && explicitArgTypes.Count != exprRef.With.Count)
			{
				throw new ParsingException(
					$"Inline expression '{exprRef.Do}' declares {explicitArgTypes.Count} ArgumentTypes " +
					$"but supplies {exprRef.With.Count} operand(s) in With.");
			}

			var argInfos = new (string type, string name)[exprRef.With.Count];
			var argTypes = new Type[exprRef.With.Count];

			for (int i = 0; i < exprRef.With.Count; i++)
			{
				var typeName = explicitArgTypes != null ? explicitArgTypes[i] : InferTypeName(ctx, exprRef.With[i]);

				if (!ctx.TypeRegistry.TryGetValue(typeName, out var argType))
				{
					throw new ParsingException(
						$"Inline expression '{exprRef.Do}' could not resolve a type for argument {i} " +
						$"('{typeName}').");
				}

				argInfos[i] = (typeName, $"arg{i}");
				argTypes[i] = argType;
			}

			var returnTypeName = exprRef.ReturnType ?? InferReturnTypeName<T>(ctx, argInfos);

			if (!ctx.TypeRegistry.ContainsKey(returnTypeName))
			{
				throw new ParsingException(
					$"Inline expression '{exprRef.Do}' has unknown ReturnType '{returnTypeName}'.");
			}

			var id = ctx.InlineExpressions.Add(generatedId => new ExpressionInfo(
				generatedId,
				argInfos,
				returnTypeName,
				exprRef.RegisterTypes?.ToArray() ?? Array.Empty<string>(),
				exprRef.RegisterTypeStatics?.ToArray() ?? Array.Empty<string>(),
				WrapInlineBody(exprRef.Do)));

			var args = new IValueSourceArg[exprRef.With.Count];

			for (int i = 0; i < exprRef.With.Count; i++)
			{
				args[i] = ValueSourceFactory.BuildArg(ctx, argTypes[i], exprRef.With[i]);
			}

			return new ExpressionSource<T>(id, args);
		}

		// Best-effort static type-name for an inline operand: literals by kind, `!var` by its
		// resolved value, nested named `!expr` by its declared return type. Anything else (including
		// nested inline `!expr`) falls back to the use-site type, supplied by the caller via the
		// generic CreateValueSource<T> through InferReturnTypeName — here we default to "float".
		private static string InferTypeName(TransformContext ctx, AssemblerValue value) =>
			value switch
			{
				IntValue => "int",
				FloatValue => "float",
				BoolValue => "bool",
				StringValue => "string",
				Vector3Value or VecValue => "vector",
				ColorValue or ColourValue => "colour",
				TypedListValue typed when TryGetTypeName(ctx,
					typeof(List<>).MakeGenericType(typed.ElementType), out var listName) => listName,
				VarRef varRef => TryResolveValue(ctx, varRef.Id, out var resolved)
					? InferTypeName(ctx, resolved)
					: "float",
				ExprRef nested when TryResolveNamedExpression(ctx, nested.Do, out var info) => info.ReturnType,
				_ => "float"
			};

		// Return type for a synthesised inline expression: the use-site T mapped to a registry name
		// where possible (the common case — Position wants vector, a float property wants float). When
		// T isn't a registered type (e.g. object), fall back to the first operand's inferred type.
		private static string InferReturnTypeName<T>(TransformContext ctx, (string type, string name)[] argInfos)
		{
			if (typeof(T) != typeof(object) && TryGetTypeName(ctx, typeof(T), out var name))
			{
				return name;
			}

			if (argInfos.Length > 0)
			{
				return argInfos[0].type;
			}

			throw new ParsingException(
				$"Cannot infer the return type of an inline !expr used as '{typeof(T).Name}'. " +
				"Use a named expression with a declared ReturnType instead.");
		}

		// Inline bodies are written as terse expressions (`-arg0`, `arg0 * 2`). The compiler parses a
		// method body of statements, so a bare expression needs an explicit `return … ;`. A body that
		// already contains a statement separator (`;`) is treated as hand-written statements and passed
		// through unchanged (it may use `return`, locals, etc., exactly like a declared expression body).
		private static string WrapInlineBody(string body) =>
			body.Contains(';') ? body : $"return {body};";

		private static bool TryGetTypeName(TransformContext ctx, Type type, out string name)
		{
			foreach (var kvp in ctx.TypeRegistry)
			{
				if (kvp.Value == type)
				{
					name = kvp.Key;
					return true;
				}
			}

			name = string.Empty;
			return false;
		}

		private static bool TryResolveValue(TransformContext ctx, string id, out AssemblerValue value)
		{
			foreach (var info in ctx.Values)
			{
				if (info.Id == id)
				{
					value = info.Value;
					return true;
				}
			}

			value = NoValue.Instance;
			return false;
		}
	}
}
