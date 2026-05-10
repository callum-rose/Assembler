using System;
using System.Reflection;
using Assembler.Parsing.Phase2.Info;

namespace Assembler.Parsing.Phase3
{
	public static class ValueResolver
	{
		public static IValueProvider<T> Resolve<T>(this ValueWrapper<T> wrapper,
			VariableRegistry variables,
			CompiledExpressionsRegistry expressions)
		{
			switch (wrapper)
			{
				case Constant<T> constant:
					return new ValueContainerProvider<T>(new ValueContainer<T>(constant.Value));

				case VariableRef<T> variableRef:
					return new ValueContainerProvider<T>(variables.Get<T>(variableRef.VariableId));

				case ExpressionRef<T> expressionRef:
					return new ExpressionContainerProvider<T>(BuildExpressionContainer(expressionRef, variables, expressions));

				default:
					throw new Exception($"Unsupported ValueWrapper type: {wrapper?.GetType()}");
			}
		}

		private static ExpressionContainer<TReturn> BuildExpressionContainer<TReturn>(
			ExpressionRef<TReturn> expressionRef,
			VariableRegistry variables,
			CompiledExpressionsRegistry expressions)
		{
			var (delegateType, @delegate) = expressions.GetCompiled(expressionRef.ExpressionId);
			var info = expressions.GetInfo(expressionRef.ExpressionId);

			var argProviders = new IValueProvider<object>[expressionRef.Arguments.Count];

			for (int i = 0; i < expressionRef.Arguments.Count; i++)
			{
				var paramType = expressions.ResolveType(info.Arguments[i].type);
				argProviders[i] = ResolveAsObject(expressionRef.Arguments[i], paramType, variables, expressions);
			}

			Func<TReturn> curried = () =>
			{
				var args = new object[argProviders.Length];

				for (int i = 0; i < argProviders.Length; i++)
				{
					args[i] = argProviders[i].Value;
				}

				return (TReturn)@delegate.DynamicInvoke(args);
			};

			return new ExpressionContainer<TReturn>(curried);
		}

		// Argument-side resolver: wrapper is ValueWrapper<object> at the surface level,
		// but underlying records may be Constant<T>/VariableRef<T>/ExpressionRef<T> for the actual T.
		private static IValueProvider<object> ResolveAsObject(
			ValueWrapper<object> wrapper,
			Type expectedType,
			VariableRegistry variables,
			CompiledExpressionsRegistry expressions)
		{
			var wrapperType = wrapper.GetType();
			var genericDef = wrapperType.GetGenericTypeDefinition();
			var innerType = wrapperType.GetGenericArguments()[0];
			var typeForResolve = innerType == typeof(object) ? expectedType : innerType;

			var method = typeof(ValueResolver).GetMethod(nameof(ResolveTyped),
				BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(typeForResolve);

			return (IValueProvider<object>)method.Invoke(null,
				new object[]
				{
					wrapper, variables, expressions
				});
		}

		private static IValueProvider<object> ResolveTyped<T>(
			object wrapperBoxed,
			VariableRegistry variables,
			CompiledExpressionsRegistry expressions)
		{
			switch (wrapperBoxed)
			{
				case Constant<T> c:
					return new BoxedProvider<T>(new ValueContainerProvider<T>(new ValueContainer<T>(c.Value)));
				case VariableRef<T> v:
					return new BoxedProvider<T>(new ValueContainerProvider<T>(variables.Get<T>(v.VariableId)));
				case ExpressionRef<T> e:
					return new BoxedProvider<T>(
						new ExpressionContainerProvider<T>(BuildExpressionContainer(e, variables, expressions)));
				// Fallback: argument was declared with object generic but holds a Constant<object>/etc.
				case Constant<object> co:
					return new ConstObjectProvider(co.Value);
				case VariableRef<object> vo:
					return new BoxedProvider<T>(new ValueContainerProvider<T>(variables.Get<T>(vo.VariableId)));
				default:
					throw new Exception($"Unsupported argument wrapper: {wrapperBoxed?.GetType()}");
			}
		}

		private sealed class BoxedProvider<T> : IValueProvider<object>
		{
			public object Value => _inner.Value;

			private readonly IValueProvider<T> _inner;

			public BoxedProvider(IValueProvider<T> inner) => _inner = inner;
		}

		private sealed class ConstObjectProvider : IValueProvider<object>
		{
			public object Value => _value;

			private readonly object _value;

			public ConstObjectProvider(object value) => _value = value;
		}
	}
}