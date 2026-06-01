using System;

namespace Assembler.Resolving
{
	/// <summary>
	/// Adapts any <see cref="IValueProvider"/> to <see cref="IValueProvider{T}"/> of object by boxing its value,
	/// so a typed variable (e.g. int) can satisfy an object-typed value reference.
	/// </summary>
	public sealed class BoxingValueProvider : IValueProvider<object>
	{
		private readonly IValueProvider _innerProvider;

		public BoxingValueProvider(IValueProvider innerProvider)
		{
			_innerProvider = innerProvider;
		}

		public object Get(TriggerContext ctx) => _innerProvider.Get(ctx);

		public void Set(object value) =>
			throw new InvalidOperationException("BoxingValueProvider cannot have its value set");
	}
}
