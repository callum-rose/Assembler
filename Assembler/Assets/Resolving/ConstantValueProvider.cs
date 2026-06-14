namespace Assembler.Resolving
{
	/// <summary>Non-generic marker: a provider whose value is fixed for the game's lifetime — never written,
	/// never invalidated. Lets type-dispatch sites recognise a constant without knowing T.</summary>
	public interface IConstantValueProvider { }

	/// <summary>An immutable literal. Unlike <see cref="ValueProvider{T}"/> it is not writable and not observable,
	/// so a live binding applies it once and binds nothing (no sink, no subscription) instead of subscribing to an
	/// Invalidated pulse that can never fire.</summary>
	public sealed class ConstantValueProvider<T> : IValueProvider<T>, IConstantValueProvider
	{
		private readonly T _value;

		public ConstantValueProvider(T value) => _value = value;

		public T Get(TriggerContext ctx) => _value;

		object IValueProvider.Get(TriggerContext ctx) => _value!;
	}
}
