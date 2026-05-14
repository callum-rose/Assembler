using System;

namespace Assembler.Resolving
{
	public interface IValueProvider<T>
	{
		/// <summary>
		/// Get and sets the inner value.
		/// Will throw when getting if there is no inner value, and will throw when setting if it's not a settable value
		/// </summary>
		T Value { get; set; }
	}

	public class NullValueProvider<T> : IValueProvider<T>
	{
		public static NullValueProvider<T> Instance { get; } = new();

		public T Value
		{
			get => throw new InvalidOperationException("Null value provider cannot provide a value");
			set => throw new InvalidOperationException("Null value provider cannot have its value set");
		}

		private NullValueProvider() { }
	}

	public sealed class ValueProvider<T> : IValueProvider<T>
	{
		public T Value { get; set; }

		public ValueProvider(T value)
		{
			Value = value;
		}

		public void Set(T value)
		{
			Value = value;
		}
	}

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