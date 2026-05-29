using System;

namespace Assembler.Resolving
{
	public static class IValueProviderExtensions
	{
		public static void UseIfValueExists<T>(this IValueProvider<T> provider, TriggerContext ctx, Action<T> action)
		{
			if (provider is not NullValueProvider<T>)
			{
				action(provider.Get(ctx));
			}
		}

		public static T ValueOr<T>(this IValueProvider<T> provider, TriggerContext ctx, T defaultValue) =>
			provider is NullValueProvider<T> ? defaultValue : provider.Get(ctx);
	}
}
