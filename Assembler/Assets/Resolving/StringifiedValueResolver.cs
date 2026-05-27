using System;
using System.Collections.Generic;
using System.Reflection;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using UnityEngine;

namespace Assembler.Resolving
{
	/// <summary>
	/// Resolves a raw <see cref="AssemblerValue"/> into an <see cref="IValueProvider{T}"/> of
	/// <see cref="string"/>, stringifying the underlying value at read-time. Used by
	/// behaviours (e.g. <c>text label</c>) whose declared property type is <c>string</c>
	/// but where authors want to live-bind a numeric, vector or boolean variable / expression.
	///
	/// Resolution strategy:
	///   - String / null / asset / trigger-output / entity-position / parameter / list /
	///     dict sources fall through to the normal <c>ValueSource&lt;string&gt;</c> path so
	///     existing behaviour is preserved.
	///   - Variable references are resolved using the variable's declared type (read from
	///     the supplied <see cref="ValueInfo"/> list) then wrapped in a stringifier.
	///   - Expression references are resolved using the expression's declared
	///     <c>ReturnType</c> then wrapped in a stringifier.
	///   - Constant primitive values (int/float/bool/vector/colour) are stringified directly.
	/// </summary>
	public static class StringifiedValueResolver
	{
		public static IValueProvider<string> Resolve(
			AssemblerValue raw,
			IReadOnlyList<ValueInfo> resolvedValues,
			VariableRegistry variables,
			CompiledExpressionsRegistry expressions,
			AssetRegistry assets,
			TriggerContext triggerContext,
			EntityVariableScope scope,
			EntityTransformRegistry entityTransforms)
		{
			switch (raw)
			{
				case null:
				case NoValue:
					return new ValueProvider<string>(string.Empty);

				case StringValue s:
					return new ValueProvider<string>(s.Value);

				case IntValue i:
					return new ValueProvider<string>(i.Value.ToString());
				case FloatValue f:
					return new ValueProvider<string>(f.Value.ToString());
				case BoolValue b:
					return new ValueProvider<string>(b.Value.ToString());
				case Vector2Value v2:
					return new ValueProvider<string>(v2.Value.ToString());
				case Vector3Value v3:
					return new ValueProvider<string>(v3.Value.ToString());
				case ColorValue c:
					return new ValueProvider<string>(c.Value.ToString());

				case VarRef varRef:
				{
					var innerType = ResolveVariableType(varRef.Id, resolvedValues);
					if (innerType == typeof(string))
					{
						return variables.Get<string>(varRef.Id, scope);
					}

					var inner = GetVariableUntyped(innerType, varRef.Id, variables, scope);
					return new StringifyingValueProvider(inner);
				}

				case ExprRef exprRef:
				{
					var info = expressions.GetInfo(exprRef.ExpressionId);
					var innerType = expressions.ResolveType(info.ReturnType);
					if (innerType == typeof(string))
					{
						var stringSource = Transformer.CreateValueSource<string>(resolvedValues, raw);
						return stringSource.Resolve(variables, expressions, assets, triggerContext, scope,
							entityTransforms);
					}

					var resolvedInner = ResolveExpressionUntyped(exprRef, innerType, resolvedValues, variables,
						expressions, assets, triggerContext, scope, entityTransforms);
					return new StringifyingValueProvider(resolvedInner);
				}

				default:
				{
					// Fall back to the existing string-typed path for AssetRef / OutputRef /
					// EntityPositionRef / ParamRef / list / dict / colour-expression etc.
					var stringSource = Transformer.CreateValueSource<string>(resolvedValues, raw);
					return stringSource.Resolve(variables, expressions, assets, triggerContext, scope,
						entityTransforms);
				}
			}
		}

		private static Type ResolveVariableType(string id, IReadOnlyList<ValueInfo> resolvedValues)
		{
			ValueInfo match = null;
			for (var i = 0; i < resolvedValues.Count; i++)
			{
				if (resolvedValues[i].Id == id)
				{
					match = resolvedValues[i];
					break;
				}
			}

			if (match is null)
			{
				throw new Exception(
					$"StringifiedValueResolver: variable '{id}' not registered (cannot infer its type for stringification)");
			}

			return match.Value switch
			{
				IntValue => typeof(int),
				FloatValue => typeof(float),
				BoolValue => typeof(bool),
				StringValue => typeof(string),
				Vector2Value => typeof(Vector2),
				Vector3Value => typeof(Vector3),
				ColorValue => typeof(Color),
				_ => throw new Exception(
					$"StringifiedValueResolver: variable '{id}' has unsupported value type {match.Value.GetType().Name} for stringification")
			};
		}

		private static readonly MethodInfo VariableRegistryGetMethod = typeof(VariableRegistry).GetMethod(
			nameof(VariableRegistry.Get),
			new[] { typeof(string), typeof(EntityVariableScope) })!;

		private static IValueProvider GetVariableUntyped(Type t, string id, VariableRegistry variables,
			EntityVariableScope scope)
		{
			var method = VariableRegistryGetMethod.MakeGenericMethod(t);
			return (IValueProvider)method.Invoke(variables, new object[] { id, scope });
		}

		private static readonly MethodInfo TransformerCreateValueSourceOpenMethod = FindCreateValueSourceMethod();

		private static MethodInfo FindCreateValueSourceMethod()
		{
			foreach (var m in typeof(Transformer).GetMethods(BindingFlags.Public | BindingFlags.Static))
			{
				if (m.Name != nameof(Transformer.CreateValueSource)) continue;
				if (!m.IsGenericMethodDefinition) continue;

				var ps = m.GetParameters();
				if (ps.Length != 3) continue;
				if (ps[0].ParameterType != typeof(IReadOnlyList<ValueInfo>)) continue;
				if (ps[1].ParameterType != typeof(AssemblerValue)) continue;

				return m;
			}

			throw new InvalidOperationException(
				"Transformer.CreateValueSource<T>(IReadOnlyList<ValueInfo>, AssemblerValue, T?) not found.");
		}

		private static readonly MethodInfo ResolveExtMethod = typeof(ValueResolver).GetMethod(nameof(ValueResolver.Resolve))!;

		private static IValueProvider ResolveExpressionUntyped(
			ExprRef exprRef,
			Type innerType,
			IReadOnlyList<ValueInfo> resolvedValues,
			VariableRegistry variables,
			CompiledExpressionsRegistry expressions,
			AssetRegistry assets,
			TriggerContext triggerContext,
			EntityVariableScope scope,
			EntityTransformRegistry entityTransforms)
		{
			var createGeneric = TransformerCreateValueSourceOpenMethod.MakeGenericMethod(innerType);
			var source = createGeneric.Invoke(null,
				new object[] { resolvedValues, exprRef, GetDefault(innerType) });

			var resolveGeneric = ResolveExtMethod.MakeGenericMethod(innerType);
			return (IValueProvider)resolveGeneric.Invoke(null,
				new object[] { source, variables, expressions, assets, triggerContext, scope, entityTransforms });
		}

		private static object GetDefault(Type t) => t.IsValueType ? Activator.CreateInstance(t) : null;
	}
}
