using System;

namespace Assembler.Resolving
{
	public sealed class MappedValueProvider<TInput, TOutput> : IValueProvider<TOutput>
	{
		public TOutput Value
		{
			get => _func.Invoke(_innerProvider.Value);
			set => throw new InvalidOperationException("MappedValueProvider cannot have its value set");
		}

		object IValueProvider.Value => Value;

		private readonly IValueProvider<TInput> _innerProvider;
		private readonly Func<TInput, TOutput> _func;

		public MappedValueProvider(IValueProvider<TInput> innerProvider, Func<TInput, TOutput> func)
		{
			_innerProvider = innerProvider;
			_func = func;
		}
	}
}
