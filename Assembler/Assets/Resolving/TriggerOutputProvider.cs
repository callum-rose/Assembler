using System;

namespace Assembler.Resolving
{
	public sealed class TriggerOutputProvider<T> : IValueProvider<T>
	{
		private readonly string _outputName;
		private readonly TriggerContextHolder _holder;

		public T Value
		{
			get => _holder.Current.Get<T>(_outputName);
			set => throw new InvalidOperationException("Cannot set a trigger output value");
		}

		object IValueProvider.Value => Value!;

		public TriggerOutputProvider(string outputName, TriggerContextHolder holder)
		{
			_outputName = outputName;
			_holder = holder;
		}
	}
}
