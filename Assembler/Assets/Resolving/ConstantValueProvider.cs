namespace Assembler.Resolving
{
	/// <summary>
	/// Non-generic marker for a provider whose value is fixed for the game's lifetime — never written, never
	/// invalidated. Lets type-dispatch sites (live binding, mapped/boxed wrappers, expression resolution)
	/// recognise a constant without knowing its <c>T</c>.
	/// </summary>
	public interface IConstantValueProvider
	{
	}

	/// <summary>
	/// An immutable literal value. Unlike <see cref="ValueProvider{T}"/> it is neither writable nor observable,
	/// so a live binding applies it once and binds nothing (no sink, no subscription) instead of subscribing to
	/// an <c>Invalidated</c> pulse that can never fire.
	/// </summary>
	public sealed class ConstantValueProvider<T> : IValueProvider<T>, IConstantValueProvider
	{
		private readonly T _value;

		public ConstantValueProvider(T value) => _value = value;

		public T Get(TriggerContext ctx) => _value;

		object IValueProvider.Get(TriggerContext ctx) => _value!;
	}
}
