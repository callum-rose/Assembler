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

		/// <summary>
		/// Set the inner value. Throws on providers that don't support being written to.
		/// </summary>
		void Set(T value);
	}
}
