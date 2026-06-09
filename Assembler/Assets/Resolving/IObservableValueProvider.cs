using System;

namespace Assembler.Resolving
{
	/// <summary>
	/// Implemented by value providers whose value can change at runtime via <see cref="IValueProvider{T}.Set"/>.
	/// Lets consumers react to writes with a push event instead of polling each frame.
	/// </summary>
	public interface IObservableValueProvider
	{
		/// <summary>Raised after the value changes. Args are the (previous, current) values, boxed.</summary>
		event Action<object, object> Changed;
	}
}
