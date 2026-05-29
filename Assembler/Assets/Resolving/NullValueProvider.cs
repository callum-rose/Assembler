using System;

namespace Assembler.Resolving
{
	public class NullValueProvider<T> : IValueProvider<T>
	{
		public static NullValueProvider<T> Instance { get; } = new();

		public T Get(TriggerContext ctx) =>
			throw new InvalidOperationException("Null value provider cannot provide a value");

		object IValueProvider.Get(TriggerContext ctx) =>
			throw new InvalidOperationException("Null value provider cannot provide a value");

		public void Set(T value) =>
			throw new InvalidOperationException("Null value provider cannot have its value set");

		private NullValueProvider() { }
	}
}
