using System;
using Assembler.Parsing.Info;
using UnityEngine;

namespace Assembler.Resolving
{
	public static class ValueResolver
	{
		public static IValueProvider<T> Resolve<T>(this ValueSource<T> valueSource,
			VariableRegistry variables,
			CompiledExpressionsRegistry expressions,
			AssetRegistry assets,
			TriggerContext triggerContext,
			EntityVariableScope scope,
			EntityTransformRegistry entityTransforms)
		{
			return valueSource switch
			{
				ConstantSource<T> constant => new ValueProvider<T>(constant.Value),
				ValueReferenceSource<T> variableRef => variables.Get<T>(variableRef.VariableId, scope),
				ExpressionSource<T> expressionRef => new ExpressionValueProvider<T>(BuildExpressionContainer(expressionRef,
					variables,
					expressions,
					assets,
					triggerContext,
					scope,
					entityTransforms)),
				AssetSource<T> assetRef => new ValueProvider<T>(assets.Get<T>(assetRef.AssetId)),
				EntityPositionSource<T> ep when typeof(T) == typeof(Vector3) =>
					(IValueProvider<T>)(object)new TransformPositionProvider(entityTransforms.Get(ep.EntityId)),
				TriggerOutputSource<T> output => new TriggerOutputProvider<T>(output.OutputName,
					triggerContext ?? throw new InvalidOperationException(
						$"TriggerContext required to resolve trigger output '{output.OutputName}'")),
				None<T> => NullValueProvider<T>.Instance,
				_ => throw new Exception($"Unsupported ValueWrapper type: {valueSource.GetType()}")
			};
		}

		private static Func<TReturn> BuildExpressionContainer<TReturn>(
			ExpressionSource<TReturn> expressionSource,
			VariableRegistry variables,
			CompiledExpressionsRegistry expressions,
			AssetRegistry assets,
			TriggerContext triggerContext,
			EntityVariableScope? scope,
			EntityTransformRegistry entityTransforms)
		{
			var (_, @delegate) = expressions.GetCompiled(expressionSource.ExpressionId);

			var resolver = new ArgResolver(
				variables, expressions, assets, triggerContext, scope, entityTransforms);

			var argProviders = new IValueProvider[expressionSource.Arguments.Count];

			for (int i = 0; i < expressionSource.Arguments.Count; i++)
			{
				argProviders[i] = (IValueProvider)expressionSource.Arguments[i].Resolve(resolver);
			}

			return InvokeWithArgs;

			TReturn InvokeWithArgs()
			{
				var args = new object[argProviders.Length];

				for (int i = 0; i < argProviders.Length; i++)
				{
					args[i] = argProviders[i].Value;
				}

				try
				{
					return (TReturn)@delegate.DynamicInvoke(args);
				}
				catch (Exception e)
				{
					throw new Exception(
						$"Failed to evaluate expression '{expressionSource.ExpressionId}' to '{typeof(TReturn)}' with arguments: {string.Join(", ", expressionSource.Arguments)}",
						e);
				}
			}
		}

		private sealed class ArgResolver : IValueSourceResolver
		{
			private readonly VariableRegistry _variables;
			private readonly CompiledExpressionsRegistry _expressions;
			private readonly AssetRegistry _assets;
			private readonly TriggerContext _triggerContext;
			private readonly EntityVariableScope? _scope;
			private readonly EntityTransformRegistry _entityTransforms;

			public ArgResolver(VariableRegistry variables,
				CompiledExpressionsRegistry expressions,
				AssetRegistry assets,
				TriggerContext triggerContext,
				EntityVariableScope? scope,
				EntityTransformRegistry entityTransforms)
			{
				_variables = variables;
				_expressions = expressions;
				_assets = assets;
				_triggerContext = triggerContext;
				_scope = scope;
				_entityTransforms = entityTransforms;
			}

			public object Resolve<T>(ValueSource<T> source) =>
				source.Resolve(_variables, _expressions, _assets, _triggerContext, _scope!, _entityTransforms);
		}
	}
}
