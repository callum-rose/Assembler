using System;

namespace Assembler.Resolving
{
	/// <summary>
	/// Adapts any <see cref="IValueProvider"/> to <see cref="IValueProvider{T}"/> of object by boxing its value,
	/// so a typed variable (e.g. int) can satisfy an object-typed value reference. Prefer <see cref="Create"/>
	/// so the result forwards the inner provider's observability when it has any — otherwise a boxed variable
	/// would silently drop onto the polled live-binding path.
	/// </summary>
	public class BoxingValueProvider : IValueProvider<object>
	{
		private readonly IValueProvider _innerProvider;

		public BoxingValueProvider(IValueProvider innerProvider)
		{
			_innerProvider = innerProvider;
		}

		/// <summary>Build a boxing provider that preserves the inner provider's binding class: a constant boxes to
		/// a constant (boxing a fixed value leaves it fixed), an observable forwards its <c>Invalidated</c> pulse,
		/// and any other inner falls back to the plain (polled) variant.</summary>
		public static IValueProvider<object> Create(IValueProvider innerProvider) =>
			innerProvider is IConstantValueProvider
				? new ConstantValueProvider<object>(innerProvider.Get(TriggerContext.Empty))
				: innerProvider is IObservableValueProvider observable
					? new ObservableBoxingValueProvider(innerProvider, observable)
					: new BoxingValueProvider(innerProvider);

		public object Get(TriggerContext ctx) => _innerProvider.Get(ctx);
	}

	/// <summary>
	/// A <see cref="BoxingValueProvider"/> over an observable inner: boxing doesn't change <em>when</em> the
	/// value changes, so it re-raises the inner's <see cref="IObservableValueProvider.Invalidated"/> pulse —
	/// letting a live binding push a boxed variable instead of polling it.
	/// </summary>
	public sealed class ObservableBoxingValueProvider : BoxingValueProvider, IObservableValueProvider
	{
		private readonly IObservableValueProvider _observableInner;

		public ObservableBoxingValueProvider(IValueProvider innerProvider, IObservableValueProvider observableInner)
			: base(innerProvider) =>
			_observableInner = observableInner;

		public event Action? Invalidated
		{
			add => _observableInner.Invalidated += value;
			remove => _observableInner.Invalidated -= value;
		}
	}
}
