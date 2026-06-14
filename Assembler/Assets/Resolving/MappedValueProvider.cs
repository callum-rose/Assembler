using System;

namespace Assembler.Resolving
{
	/// <summary>
	/// Adapts an <see cref="IValueProvider{TInput}"/> to <see cref="IValueProvider{TOutput}"/> through a pure
	/// mapping function (e.g. reading an <c>int</c> variable as a <c>float</c>). Prefer <see cref="Create"/>:
	/// because the map is pure, the result changes exactly when the inner provider changes, so when the inner is
	/// observable the wrapper can forward its <c>Invalidated</c> pulse — otherwise a pushed source (a variable)
	/// wrapped here would silently drop onto the polled live-binding path.
	/// </summary>
	public class MappedValueProvider<TInput, TOutput> : IValueProvider<TOutput>
	{
		private readonly IValueProvider<TInput> _innerProvider;
		private readonly Func<TInput, TOutput> _func;

		public MappedValueProvider(IValueProvider<TInput> innerProvider, Func<TInput, TOutput> func)
		{
			_innerProvider = innerProvider;
			_func = func;
		}

		/// <summary>Build a mapped provider that preserves the inner provider's binding class: a constant maps to
		/// a constant (the map of a fixed value is itself fixed), an observable forwards its <c>Invalidated</c>
		/// pulse, and any other inner falls back to the plain (polled) variant.</summary>
		public static IValueProvider<TOutput> Create(IValueProvider<TInput> innerProvider, Func<TInput, TOutput> func) =>
			innerProvider is IConstantValueProvider
				? new ConstantValueProvider<TOutput>(func(innerProvider.Get()))
				: innerProvider is IObservableValueProvider observable
					? new ObservableMappedValueProvider<TInput, TOutput>(innerProvider, observable, func)
					: new MappedValueProvider<TInput, TOutput>(innerProvider, func);

		public TOutput Get(TriggerContext ctx) => _func(_innerProvider.Get(ctx));

		object IValueProvider.Get(TriggerContext ctx) => Get(ctx)!;
	}

	/// <summary>
	/// A <see cref="MappedValueProvider{TInput,TOutput}"/> over an observable inner. The pure map means the
	/// mapped value changes only when the inner does, so this re-raises the inner's
	/// <see cref="IObservableValueProvider.Invalidated"/> pulse verbatim — letting a live binding push a mapped
	/// variable (e.g. an <c>int</c> <c>!var</c> read as a <c>float</c>) instead of polling it.
	/// </summary>
	public sealed class ObservableMappedValueProvider<TInput, TOutput>
		: MappedValueProvider<TInput, TOutput>, IObservableValueProvider
	{
		private readonly IObservableValueProvider _observableInner;

		public ObservableMappedValueProvider(
			IValueProvider<TInput> innerProvider,
			IObservableValueProvider observableInner,
			Func<TInput, TOutput> func) : base(innerProvider, func) =>
			_observableInner = observableInner;

		public event Action? Invalidated
		{
			add => _observableInner.Invalidated += value;
			remove => _observableInner.Invalidated -= value;
		}
	}
}
