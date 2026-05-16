using System;

namespace Assembler.Resolving
{
	public sealed class ExpressionValueProvider<T> : IValueProvider<T>
	{
		public T Value
		{
			get => _func.Invoke();
			set => throw new InvalidOperationException("ExpressionContainerProvider cannot have its value set");
		}

		private readonly Func<T> _func;

		public ExpressionValueProvider(Func<T> func)
		{
			_func = func;
		}
	}
}