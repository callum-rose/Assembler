using System;
using Assembler.Parsing.Info;
using UnityEngine;

namespace Assembler.Resolving
{
	public static class ValueResolver
	{
		public static IValueProvider<T> Resolve<T>(this ValueSource<T> valueSource, ResolutionContext ctx)
		{
			return valueSource switch
			{
				ConstantSource<T> constant => new ValueProvider<T>(constant.Value),
				ValueReferenceSource<T> variableRef => ctx.Variables.Get<T>(variableRef.VariableId, ctx.Scope),
				ExpressionSource<T> expressionRef => new ExpressionValueProvider<T>(BuildExpressionContainer(expressionRef, ctx)),
				AssetSource<T> assetRef => new ValueProvider<T>(ctx.Assets.Get<T>(assetRef.AssetId)),
				EntityPositionSource<T> ep when typeof(T) == typeof(Vector3) =>
					(IValueProvider<T>)(object)new TransformPositionProvider(ctx.EntityTransforms.Get(ep.EntityId)),
				TriggerOutputSource<T> output => new TriggerOutputProvider<T>(output.OutputName,
					ctx.ContextHolder ?? throw new InvalidOperationException(
						$"!output '{output.OutputName}' resolved without a TriggerContextHolder — this behaviour was built outside GameBehaviourFactory")),
				None<T> => NullValueProvider<T>.Instance,
				_ => throw new Exception($"Unsupported ValueWrapper type: {valueSource.GetType()}")
			};
		}

		private static Func<TReturn> BuildExpressionContainer<TReturn>(
			ExpressionSource<TReturn> expressionSource,
			ResolutionContext ctx)
		{
			var (_, @delegate) = ctx.Expressions.GetCompiled(expressionSource.ExpressionId);

			var resolver = new ArgResolver(ctx);

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
			private readonly ResolutionContext _ctx;

			public ArgResolver(ResolutionContext ctx)
			{
				_ctx = ctx;
			}

			public object Resolve<T>(ValueSource<T> source) => source.Resolve(_ctx);
		}
	}
}
