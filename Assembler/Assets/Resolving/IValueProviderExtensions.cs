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

		/// <summary>
		/// Narrow a provider to <see cref="IWriteValueProvider{T}"/>, throwing a <see cref="ResolveException"/>
		/// when the source is read-only. Used at build time so wiring a read-only value (a constant, expression,
		/// clock, …) into a writable slot fails with a named error instead of throwing on the first <c>Set</c>.
		/// An unwired slot resolves to <see cref="NullValueProvider{T}"/>, which is writable (no-op), so it passes.
		/// </summary>
		public static IWriteValueProvider<T> AsWritable<T>(this IValueProvider<T> provider) =>
			provider as IWriteValueProvider<T>
			?? throw new ResolveException(
				$"Expected a writable value provider but '{provider.GetType().Name}' is read-only.");

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
