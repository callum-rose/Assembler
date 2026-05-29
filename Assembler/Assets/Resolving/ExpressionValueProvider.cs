using System;

namespace Assembler.Resolving
{
	public sealed class ExpressionValueProvider<T> : IValueProvider<T>
	{
		private readonly Func<TriggerContext, T> _func;

		public ExpressionValueProvider(Func<TriggerContext, T> func)
		{
			_func = func;
		}

		public T Get(TriggerContext ctx) => _func(ctx);

		object IValueProvider.Get(TriggerContext ctx) => _func(ctx)!;

		public void Set(T value) =>
			throw new InvalidOperationException("ExpressionValueProvider cannot have its value set");
	}
}
