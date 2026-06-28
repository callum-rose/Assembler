using System;
using System.Collections.Generic;

namespace Assembler.Resolving
{
	/// <summary>
	/// An expression provider that is itself observable. An <c>!expr</c> is pure over its declared argument
	/// providers — it can only see variables <em>through</em> its args, never captured internally — so its value
	/// can only change when one of those args changes. When every arg is observable this provider subscribes to
	/// each arg's <see cref="IObservableValueProvider.Invalidated"/> pulse, recomputes on any pulse, and raises
	/// its own <see cref="Invalidated"/> when the result actually changed (caching the last value to suppress
	/// no-op pulses). This turns a would-be-polled expression into a push one. Constant args are
	/// observable-but-silent, so an all-constant expression subscribes yet never fires.
	/// </summary>
	public sealed class ObservableExpressionValueProvider<T> : IValueProvider<T>, IObservableValueProvider, IDisposable
	{
		private readonly Func<TriggerContext, T> _func;
		private readonly IReadOnlyList<IObservableValueProvider> _observableArgs;
		private T _last = default!;
		private bool _hasLast;

		public ObservableExpressionValueProvider(
			Func<TriggerContext, T> func,
			IReadOnlyList<IObservableValueProvider> observableArgs)
		{
			_func = func;
			_observableArgs = observableArgs;

			foreach (var arg in _observableArgs)
			{
				arg.Invalidated += OnArgInvalidated;
			}
		}

		public event Action? Invalidated;

		public T Get(TriggerContext ctx)
		{
			var value = _func(ctx);
			_last = value;
			_hasLast = true;
			return value;
		}

		object IValueProvider.Get(TriggerContext ctx) => Get(ctx)!;

		/// <summary>Unsubscribe from every arg. The arg providers (variables) are game-global and outlive a
		/// destroyed entity, so a per-entity binding that disposes this provider must unhook it here.</summary>
		public void Dispose()
		{
			foreach (var arg in _observableArgs)
			{
				arg.Invalidated -= OnArgInvalidated;
			}
		}

		private void OnArgInvalidated()
		{
			var previous = _last;
			var hadLast = _hasLast;

			var current = _func(TriggerContext.Empty);
			_last = current;
			_hasLast = true;

			if (!hadLast || !EqualityComparer<T>.Default.Equals(previous, current))
			{
				Invalidated?.Invoke();
			}
		}
	}
}
