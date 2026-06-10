namespace Assembler.Resolving
{
	public interface IValueProvider
	{
		/// <summary>
		/// Get the inner value boxed as an object, using <paramref name="ctx"/> when the value depends on a
		/// trigger output. Most providers ignore <paramref name="ctx"/>. Throws if there is no inner value.
		/// </summary>
		object Get(TriggerContext ctx);
	}

	public interface IValueProvider<T> : IValueProvider
	{
		/// <summary>
		/// Get the inner value, using <paramref name="ctx"/> when the value depends on a trigger output.
		/// Most providers ignore <paramref name="ctx"/>. Throws if there is no inner value.
		/// </summary>
		new T Get(TriggerContext ctx);
	}

	/// <summary>
	/// A value provider whose value can be written back via <see cref="Set"/>. Variables expose this;
	/// computed/read-only providers (expressions, clock, localised text, trigger outputs) do not — so a
	/// behaviour that needs to write declares <see cref="IWriteValueProvider{T}"/> and the type system
	/// guarantees it was handed a writable source rather than relying on a runtime throw from <c>Set</c>.
	/// </summary>
	public interface IWriteValueProvider<T> : IValueProvider<T>
	{
		/// <summary>Set the inner value.</summary>
		void Set(T value);
	}
}
