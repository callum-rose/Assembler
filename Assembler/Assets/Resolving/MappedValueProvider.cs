using System;

namespace Assembler.Resolving
{
	public sealed class MappedValueProvider<TInput, TOutput> : IValueProvider<TOutput>
	{
		private readonly IValueProvider<TInput> _innerProvider;
		private readonly Func<TInput, TOutput> _func;

		public MappedValueProvider(IValueProvider<TInput> innerProvider, Func<TInput, TOutput> func)
		{
			_innerProvider = innerProvider;
			_func = func;
		}

		public TOutput Get(TriggerContext ctx) => _func(_innerProvider.Get(ctx));

		object IValueProvider.Get(TriggerContext ctx) => Get(ctx)!;

		public void Set(TOutput value) =>
			throw new InvalidOperationException("MappedValueProvider cannot have its value set");
	}
}
