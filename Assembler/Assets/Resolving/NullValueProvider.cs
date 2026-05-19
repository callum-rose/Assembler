using System;

namespace Assembler.Resolving
{
	public class NullValueProvider<T> : IValueProvider<T>
	{
		public static NullValueProvider<T> Instance { get; } = new();

		public T Value
		{
			get => throw new InvalidOperationException("Null value provider cannot provide a value");
			set => throw new InvalidOperationException("Null value provider cannot have its value set");
		}

		object IValueProvider.Value =>
			throw new InvalidOperationException("Null value provider cannot provide a value");

		private NullValueProvider() { }
	}
}
