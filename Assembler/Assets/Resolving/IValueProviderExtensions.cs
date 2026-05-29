using System;

namespace Assembler.Resolving
{
	public static class IValueProviderExtensions
	{
		/// <summary>
		/// Read the provider's value in a context where no upstream trigger is in scope (Unity callbacks,
		/// <c>OnInitialise</c>, gizmo drawing, …). Equivalent to <c>provider.Get(TriggerContext.Empty)</c>.
		/// Trigger-driven <c>Execute(ctx)</c> paths should pass the ctx through explicitly instead.
		/// </summary>
		public static T Get<T>(this IValueProvider<T> provider) => provider.Get(TriggerContext.Empty);

		public static void UseIfValueExists<T>(this IValueProvider<T> provider, TriggerContext ctx, Action<T> action)
		{
			if (provider is not NullValueProvider<T>)
			{
				action(provider.Get(ctx));
			}
		}

		public static void UseIfValueExists<T>(this IValueProvider<T> provider, Action<T> action) =>
			provider.UseIfValueExists(TriggerContext.Empty, action);

		public static T ValueOr<T>(this IValueProvider<T> provider, TriggerContext ctx, T defaultValue) =>
			provider is NullValueProvider<T> ? defaultValue : provider.Get(ctx);

		public static T ValueOr<T>(this IValueProvider<T> provider, T defaultValue) =>
			provider.ValueOr(TriggerContext.Empty, defaultValue);
	}
}
