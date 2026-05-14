using System;

namespace Assembler.Resolving
{
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
}