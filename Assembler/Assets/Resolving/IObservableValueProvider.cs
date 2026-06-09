namespace Assembler.Resolving
{
	/// <summary>Handles a value change, receiving the <paramref name="previous"/> and <paramref name="current"/> values.</summary>
	public delegate void ValueChangedHandler<T>(T previous, T current);

	/// <summary>
	/// Implemented by value providers whose value can change at runtime via <see cref="IValueProvider{T}.Set"/>.
	/// Lets consumers react to writes with a push event instead of polling each frame. Generic so a subscriber
	/// can confirm at compile time that it is observing a variable of the expected type.
	/// </summary>
	public interface IObservableValueProvider<T>
	{
		/// <summary>Raised after the value changes, with the previous and current values.</summary>
		event ValueChangedHandler<T> Changed;
	}
}
