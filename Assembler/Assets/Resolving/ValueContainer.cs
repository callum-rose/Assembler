using System;
using System.Collections.Generic;

namespace Assembler.Resolving
{
	public abstract class ValueContainer { }
	
	public sealed class ValueContainer<T> : ValueContainer
	{
		public event Action<T>? ValueChanged;

		public T Value
		{
			get => _value;
			set
			{
				if (EqualityComparer<T>.Default.Equals(_value, value))
				{
					return;
				}

				_value = value;
				ValueChanged?.Invoke(_value);
			}
		}

		private T _value;

		public ValueContainer(T initialValue)
		{
			_value = initialValue;
		}
	}
}