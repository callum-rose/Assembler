namespace Assembler.Resolving
{
	public interface IValueProvider<T>
	{
		/// <summary>
		/// Get and sets the inner value.
		/// Will throw when getting if there is no inner value, and will throw when setting if it's not a settable value
		/// </summary>
		T Value { get; set; }
	}

}