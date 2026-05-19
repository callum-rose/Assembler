namespace Assembler.Resolving
{
	public interface IValueProvider
	{
		/// <summary>
		/// Get the inner value boxed as an object. Will throw if there is no inner value.
		/// </summary>
		object Value { get; }
	}

	public interface IValueProvider<T> : IValueProvider
	{
		/// <summary>
		/// Get and sets the inner value.
		/// Will throw when getting if there is no inner value, and will throw when setting if it's not a settable value
		/// </summary>
		new T Value { get; set; }
	}
}
