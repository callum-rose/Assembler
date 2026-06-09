using System;
using System.Collections.Generic;

namespace Assembler.Resolving
{
	public sealed class ValueProvider<T> : IValueProvider<T>, IObservableValueProvider
	{
		private T _value;

		public ValueProvider(T value)
		{
			_value = value;
		}

		/// <summary>Raised after <see cref="Set"/> changes the value to a different one. Args are (previous, current).</summary>
		public event Action<object, object>? Changed;

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
			Changed?.Invoke(previous!, value!);
		}
	}
}
