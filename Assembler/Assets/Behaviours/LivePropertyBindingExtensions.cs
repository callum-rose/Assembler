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
		/// <item><b>Push</b> — an observable provider (variable, constant, or an all-observable-arg expression)
		/// applies the initial value then re-applies on each <see cref="IObservableValueProvider.Invalidated"/>
		/// pulse. Constants/all-constant expressions subscribe but never fire, so they cost nothing per frame.</item>
		/// <item><b>Poll</b> — any other provider (clock/query/transform/partial expression) re-reads each frame
		/// via the shared <see cref="LivePropertyUpdater"/> and re-applies only when the value actually changed.</item>
		/// </list>
		/// The binding is torn down when <paramref name="owner"/>'s GameObject is destroyed.
		/// </summary>
		public static void BindLive<T>(this IValueProvider<T> provider,
			GameBehaviour owner, Action<T> apply, T fallback)
		{
			if (provider is NullValueProvider<T>)
			{
				apply(fallback);
				return;
			}

			apply(provider.Get(TriggerContext.Empty));

			var sink = owner.gameObject.GetOrAddComponent<LivePropertyBindings>();

			void Reapply() => apply(provider.Get(TriggerContext.Empty));

			if (provider is IObservableValueProvider observable)
			{
				observable.Invalidated += Reapply;
				sink.Add(() =>
				{
					observable.Invalidated -= Reapply;

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

			var last = provider.Get(TriggerContext.Empty);
			sink.Add(owner.LiveProperties.Register(() =>
			{
				var next = provider.Get(TriggerContext.Empty);
				if (!EqualityComparer<T>.Default.Equals(next, last))
				{
					last = next;
					apply(next);
				}
			}));
		}
	}
}
