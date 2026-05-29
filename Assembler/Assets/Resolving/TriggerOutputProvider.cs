using System;

namespace Assembler.Resolving
{
	public sealed class TriggerOutputProvider<T> : IValueProvider<T>
	{
		private readonly string _outputName;

		public T Value
		{
			get
			{
				var ctx = TriggerContextScope.Current
					?? throw new InvalidOperationException(
						$"!output '{_outputName}' read outside of any trigger notification scope");

				return ctx.Get<T>(_outputName);
			}
			set => throw new InvalidOperationException("Cannot set a trigger output value");
		}

		object IValueProvider.Value => Value!;

		public TriggerOutputProvider(string outputName)
		{
			_outputName = outputName;
		}
	}
}
