using System;
using System.Linq;
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
				EntityPropertySource<T> ep when typeof(T) == typeof(Vector3) =>
					(IValueProvider<T>)(object)new TransformPropertyProvider(ctx.EntityTransforms.Get(ep.EntityId), ep.Property),
				RigidbodyPropertySource<T> rb when typeof(T) == typeof(Vector3) =>
					(IValueProvider<T>)(object)new RigidbodyPropertyProvider(ctx.EntityTransforms.Get(rb.EntityId), rb.Property),
				ClockValueSource<T> clock => new ClockValueProvider<T>(ctx.Clock, clock.Property),
				QuerySource<T> query => new QueryValueProvider<T>(ctx.EntityQuery, query.Kind, query.EntityTag,
					query.Origin.Resolve(ctx), query.MaxRange.Resolve(ctx)),
				LocalisedTextSource<T> text when typeof(T) == typeof(string) =>
					(IValueProvider<T>)(object)BuildLocalisedTextProvider(text, ctx),
				TriggerOutputSource<T> output => new TriggerOutputProvider<T>(output.OutputName),
				None<T> => NullValueProvider<T>.Instance,
				// SubstituteParameters is meant to eliminate every ParameterSource during template
				// instantiation. If one survives to resolve time, a template parameter went unsubstituted —
				// name it rather than letting it hit the opaque "Unsupported" catch-all below.
				ParameterSource<T> parameter => throw new ResolveException(
					$"Unsubstituted template parameter '{parameter.ParameterId}' reached resolve time — " +
					"it should have been substituted during template instantiation."),
				_ => throw new ResolveException($"Unsupported ValueSource type: {valueSource.GetType()}")
			};
		}

		/// <summary>
		/// Resolve a value source that a behaviour needs to write to, narrowing the result to
		/// <see cref="IWriteValueProvider{T}"/>. Throws a <see cref="ResolveException"/> at build time if the
		/// source resolves to a read-only provider (e.g. a constant or expression wired into a writable slot).
		/// </summary>
		public static IWriteValueProvider<T> ResolveWritable<T>(this ValueSource<T> valueSource, ResolutionContext ctx) =>
			valueSource.Resolve(ctx).AsWritable();

		private static LocalisedTextProvider BuildLocalisedTextProvider<T>(
			LocalisedTextSource<T> source,
			ResolutionContext ctx)
		{
			var resolver = new ArgResolver(ctx);

			var argProviders = source.Arguments
				.Select(arg => (IValueProvider)arg.Resolve(resolver))
				.ToArray();

			return new LocalisedTextProvider(ctx.Strings, source.Key, argProviders);
		}

		private static Func<TriggerContext, TReturn> BuildExpressionContainer<TReturn>(
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

			TReturn InvokeWithArgs(TriggerContext triggerCtx)
			{
				var args = new object[argProviders.Length];

				for (int i = 0; i < argProviders.Length; i++)
				{
					args[i] = argProviders[i].Get(triggerCtx);
				}

				try
				{
					return (TReturn)@delegate.DynamicInvoke(args);
				}
				catch (Exception e)
				{
					throw new ResolveException(
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
