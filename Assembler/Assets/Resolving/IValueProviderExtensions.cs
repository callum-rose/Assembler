using System;

namespace Assembler.Resolving
{
	public static class IValueProviderExtensions
	{
		public static void UseIfValueExists<T>(this IValueProvider<T> provider, Action<T> action)
		{
			if (provider is not NullValueProvider<T>)
			{
				action(provider.Value);
			}
		}

		public static T ValueOr<T>(this IValueProvider<T> provider, T defaultValue) =>
			provider is NullValueProvider<T> ? defaultValue : provider.Value;
	}
}