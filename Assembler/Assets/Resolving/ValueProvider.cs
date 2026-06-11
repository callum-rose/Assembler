using System;
using System.Collections.Generic;

namespace Assembler.Resolving
{
	public sealed class ValueProvider<T> : IWriteValueProvider<T>, IObservableValueProvider<T>
	{
		private T _value;

		public ValueProvider(T value)
		{
			_value = value;
		}

		/// <summary>Raised after <see cref="Set"/> changes the value to a different one, with the previous and current values.</summary>
		public event ValueChangedHandler<T>? Changed;

		/// <summary>Untyped "I may have changed" pulse raised alongside <see cref="Changed"/> on a real write.
		/// A constant (never <see cref="Set"/>) is observable-but-silent: subscribers pay nothing.</summary>
		public event Action? Invalidated;

		public T Get(TriggerContext ctx) => _value;

		object IValueProvider.Get(TriggerContext ctx) => _value!;

		public void Set(T value)
		{
			if (EqualityComparer<T>.Default.Equals(_value, value))
			{
				return;
			}

			var previous = _value;
			_value = value;
			Changed?.Invoke(previous, value);
			Invalidated?.Invoke();
		}
	}
}
