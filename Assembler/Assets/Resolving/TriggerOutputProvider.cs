using System;

namespace Assembler.Resolving
{
	public sealed class TriggerOutputProvider<T> : IValueProvider<T>
	{
		private readonly string _outputName;

		public TriggerOutputProvider(string outputName)
		{
			_outputName = outputName;
		}

		public T Get(TriggerContext ctx) => ctx.Get<T>(_outputName);

		object IValueProvider.Get(TriggerContext ctx) => ctx.Get<T>(_outputName)!;

		public void Set(T value) =>
			throw new InvalidOperationException("Cannot set a trigger output value");
	}
}
