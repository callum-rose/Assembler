using System;
using System.Reflection;
using Assembler.Parsing.Info;

namespace Assembler.Resolving
{
	public static class ValueResolver
	{
		public static IValueProvider<T> Resolve<T>(this ValueSource<T> valueSource,
			VariableRegistry variables,
			CompiledExpressionsRegistry expressions,
			AssetRegistry assets,
			TriggerContext triggerContext = null)
		{
			return valueSource switch
			{
				ConstantSource<T> constant => new ValueProvider<T>(constant.Value),
				ValueReferenceSource<T> variableRef => variables.Get<T>(variableRef.VariableId),
				ExpressionSource<T> expressionRef => new ExpressionValueProvider<T>(
					BuildExpressionContainer(expressionRef, variables, expressions, triggerContext)),
				AssetSource<T> assetRef => new ValueProvider<T>(assets.Get<T>(assetRef.AssetId)),
				TriggerOutputSource<T> output => new TriggerOutputProvider<T>(output.OutputName,
					triggerContext ?? throw new InvalidOperationException(
						$"TriggerContext required to resolve trigger output '{output.OutputName}'")),
				None<T> => NullValueProvider<T>.Instance,
				_ => throw new Exception($"Unsupported ValueWrapper type: {valueSource?.GetType()}")
			};
		}

		private static Func<TReturn> BuildExpressionContainer<TReturn>(
			ExpressionSource<TReturn> expressionSource,
			VariableRegistry variables,
			CompiledExpressionsRegistry expressions,
			TriggerContext triggerContext)
		{
			var (delegateType, @delegate) = expressions.GetCompiled(expressionSource.ExpressionId);
			var info = expressions.GetInfo(expressionSource.ExpressionId);

			var argProviders = new IValueProvider[expressionSource.Arguments.Count];

			for (int i = 0; i < expressionSource.Arguments.Count; i++)
			{
				var paramType = expressions.ResolveType(info.Arguments[i].type);

				argProviders[i] = ResolveAsObject(expressionSource.Arguments[i],
					paramType,
					variables,
					expressions,
					triggerContext);
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

		// Argument-side resolver: wrapper is ValueWrapper<object> at the surface level,
		// but underlying records may be Constant<T>/VariableRef<T>/ExpressionRef<T> for the actual T.
		private static IValueProvider ResolveAsObject(
			ValueSource<object> valueSource,
			Type expectedType,
			VariableRegistry variables,
			CompiledExpressionsRegistry expressions,
			TriggerContext triggerContext)
		{
			var wrapperType = valueSource.GetType();
			var innerType = wrapperType.GetGenericArguments()[0];
			var typeForResolve = innerType == typeof(object) ? expectedType : innerType;

			var method = typeof(ValueResolver).GetMethod(nameof(ResolveTyped),
				BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(typeForResolve);

			return (IValueProvider)method.Invoke(null,
				new object[]
				{
					valueSource, variables, expressions, triggerContext
				});
		}

		private static IValueProvider ResolveTyped<T>(
			object wrapperBoxed,
			VariableRegistry variables,
			CompiledExpressionsRegistry expressions,
			TriggerContext triggerContext)
		{
			return wrapperBoxed switch
			{
				ConstantSource<T> c => new ValueProvider<T>(c.Value),
				ConstantSource<object> co => new ConstObjectProvider(co.Value),
				ValueReferenceSource<T> v => variables.Get<T>(v.VariableId),
				ValueReferenceSource<object> vo => variables.Get<T>(vo.VariableId),
				ExpressionSource<T> e => new ExpressionValueProvider<T>(
					BuildExpressionContainer(e, variables, expressions, triggerContext)),
				TriggerOutputSource<T> o => new TriggerOutputProvider<T>(o.OutputName,
					triggerContext ?? throw new InvalidOperationException(
						$"TriggerContext required to resolve trigger output '{o.OutputName}'")),
				TriggerOutputSource<object> o => new TriggerOutputProvider<T>(o.OutputName,
					triggerContext ?? throw new InvalidOperationException(
						$"TriggerContext required to resolve trigger output '{o.OutputName}'")),
				_ => throw new Exception($"Unsupported argument wrapper: {wrapperBoxed.GetType()}")
			};
		}

		private sealed class ConstObjectProvider : IValueProvider
		{
			public object Value => _value;

			private readonly object _value;

			public ConstObjectProvider(object value) => _value = value;
		}
	}
}
