using System;

namespace Assembler.Parsing.Phase3
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

	public sealed class ValueContainerProvider<T> : IValueProvider<T>
	{
		public T Value
		{
			get => _container.Value;
			set => _container.Value = value;
		}

		private readonly ValueContainer<T> _container;

		public ValueContainerProvider(ValueContainer<T> container)
		{
			_container = container;
		}
	}

	public sealed class ExpressionContainerProvider<T> : IValueProvider<T>
	{
		public T Value
		{
			get => _container.Invoke();
			set => throw new InvalidOperationException("ExpressionContainerProvider cannot have its value set");
		}

		private readonly ExpressionContainer<T> _container;

		public ExpressionContainerProvider(ExpressionContainer<T> container)
		{
			_container = container;
		}
	}

	public sealed class MappedValueProvider<TInput, TOutput> : IValueProvider<TOutput>
	{
		public TOutput Value
		{
			get => _mapper(_inner.Value);
			set => throw new InvalidOperationException("MappedValueProvider cannot have its value set");
		}

		private readonly IValueProvider<TInput> _inner;
		private readonly Func<TInput, TOutput> _mapper;

		public MappedValueProvider(IValueProvider<TInput> inner, Func<TInput, TOutput> mapper)
		{
			_inner = inner;
			_mapper = mapper;
		}
	}
}