using System;
using System.Collections.Generic;
using Assembler.Extensions;
using Assembler.Resolving;

namespace Assembler.Behaviours
{
	public static class LivePropertyBindingExtensions
	{
		/// <summary>
		/// Drive a component property from a value provider, re-applying it as the source changes. The routing
		/// (push / poll / none) is chosen automatically from the resolved provider type — no descriptor wiring,
		/// no reflection:
		/// <list type="bullet">
		/// <item><b>None</b> — a <see cref="NullValueProvider{T}"/> (omitted optional) applies
		/// <paramref name="fallback"/> once and binds nothing, preserving today's <c>ValueOr</c> default.</item>
		/// <item><b>Constant</b> — a <see cref="IConstantValueProvider"/> (inline literal) applies its value once
		/// and binds nothing: no sink, no subscription, since it can never change.</item>
		/// <item><b>Push</b> — an observable provider (variable, or an expression whose args are all observable or
		/// constant) applies the initial value then re-applies on each
		/// <see cref="IObservableValueProvider.Invalidated"/> pulse. An all-constant expression is
		/// observable-but-silent — it subscribes to nothing and never fires, so it costs nothing per frame.</item>
		/// <item><b>Poll</b> — any other provider (clock/query/transform/partial expression) re-reads each frame
		/// via the shared <see cref="LivePropertyUpdater"/> and re-applies only when the value actually changed.</item>
		/// </list>
		/// The binding is torn down when <paramref name="owner"/>'s GameObject is destroyed.
		/// </summary>
		public static void BindLive<T, TOwner>(this IValueProvider<T> provider,
			TOwner owner, Action<T> apply, T fallback)
			where TOwner : GameBehaviour, INeedsLiveProperties
		{
			if (provider is NullValueProvider<T>)
			{
				apply(fallback);
				return;
			}

			void Apply() => apply(provider.Get());

			// A plain immutable constant never changes: apply once and bind nothing — no sink, no subscription.
			if (provider is IConstantValueProvider)
			{
				Apply();
				return;
			}

			Apply();

			var sink = owner.gameObject.GetOrAddComponent<LivePropertyBindings>();

			if (provider is IObservableValueProvider observable)
			{
				observable.Invalidated += Apply;
				sink.Add(() =>
				{
					observable.Invalidated -= Apply;

					// An ObservableExpressionValueProvider keeps subscriptions to its (game-global) arg
					// variables; dispose it so a destroyed entity's expression unhooks too. Variables/constants
					// are not IDisposable, so this no-ops for them.
					if (provider is IDisposable disposable)
					{
						disposable.Dispose();
					}
				});

				return;
			}

			var last = provider.Get();
			sink.Add(owner.LiveProperties.Register(() =>
			{
				var next = provider.Get();
				if (!EqualityComparer<T>.Default.Equals(next, last))
				{
					last = next;
					apply(next);
				}
			}));
		}
	}
}
