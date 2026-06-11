using System;

namespace Assembler.Resolving
{
	/// <summary>Handles a value change, receiving the <paramref name="previous"/> and <paramref name="current"/> values.</summary>
	public delegate void ValueChangedHandler<T>(T previous, T current);

	/// <summary>
	/// Non-generic facet of an observable provider: an untyped "I may have changed, re-read me" pulse. This is
	/// all a live-binding consumer needs — it re-reads the current value via <see cref="IValueProvider.Get"/>
	/// rather than receiving it, so it does not care about the value's type. The typed
	/// <see cref="IObservableValueProvider{T}.Changed"/> stays for consumers that need the before/after values
	/// (e.g. <c>VariableChangedTrigger</c>).
	/// </summary>
	public interface IObservableValueProvider
	{
		/// <summary>Raised when the value may have changed; subscribers should re-read via <c>Get</c>.</summary>
		event Action Invalidated;
	}

	/// <summary>
	/// Implemented by value providers whose value can change at runtime via <see cref="IWriteValueProvider{T}.Set"/>.
	/// Lets consumers react to writes with a push event instead of polling each frame. Generic so a subscriber
	/// can confirm at compile time that it is observing a variable of the expected type.
	/// </summary>
	public interface IObservableValueProvider<T> : IObservableValueProvider
	{
		/// <summary>Raised after the value changes, with the previous and current values.</summary>
		event ValueChangedHandler<T> Changed;
	}
}
