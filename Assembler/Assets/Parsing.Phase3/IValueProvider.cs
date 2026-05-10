using System;

namespace Assembler.Parsing.Phase3
{
	public interface IValueProvider<out T>
	{
		/// <summary>
		/// The provided value. Use when sure the value exists, some implementations may throw.
		/// </summary>
		T Value { get; }
	}

	public class NullValueProvider<T> : IValueProvider<T>
	{
		public static NullValueProvider<T> Instance { get; } = new();

		public T Value => throw new InvalidOperationException("Null value provider cannot provide a value");

		private NullValueProvider() { }
	}

	public static class IValueProviderExtensions
	{
		public static IValueProvider<TOutput> Map<TInput, TOutput>(this IValueProvider<TInput> provider,
			Func<TInput, TOutput> mapper) => new MappedValueProvider<TInput, TOutput>(provider, mapper);

		public static void UseIfValueExists<T>(this IValueProvider<T> provider, Action<T> action)
		{
			if (provider is not NullValueProvider<T>)
			{
				action(provider.Value);
			}
		}
	}
	
	public sealed class ValueProvider<T> : IValueProvider<T>
	{
		public T Value { get; }

		public ValueProvider(T value)
		{
			Value = value;
		}
	}

	public sealed class ValueContainerProvider<T> : IValueProvider<T>
	{
		public T Value => _container.Value;

		private readonly ValueContainer<T> _container;

		public ValueContainerProvider(ValueContainer<T> container)
		{
			_container = container;
		}
	}

	public sealed class ExpressionContainerProvider<T> : IValueProvider<T>
	{
		public T Value => _container.Invoke();

		private readonly ExpressionContainer<T> _container;

		public ExpressionContainerProvider(ExpressionContainer<T> container)
		{
			_container = container;
		}
	}

	public sealed class MappedValueProvider<TInput, TOutput> : IValueProvider<TOutput>
	{
		public TOutput Value => _mapper(_inner.Value);

		private readonly IValueProvider<TInput> _inner;
		private readonly Func<TInput, TOutput> _mapper;

		public MappedValueProvider(IValueProvider<TInput> inner, Func<TInput, TOutput> mapper)
		{
			_inner = inner;
			_mapper = mapper;
		}
	}
}