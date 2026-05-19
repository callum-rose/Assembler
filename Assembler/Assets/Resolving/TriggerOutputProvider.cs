using System;

namespace Assembler.Resolving
{
	public sealed class TriggerOutputProvider<T> : IValueProvider<T>
	{
		private readonly string _outputName;
		private readonly TriggerContext _context;

		public T Value
		{
			get => _context.Get<T>(_outputName);
			set => throw new InvalidOperationException("Cannot set a trigger output value");
		}

		object IValueProvider.Value => Value!;

		public TriggerOutputProvider(string outputName, TriggerContext context)
		{
			_outputName = outputName;
			_context = context;
		}
	}
}
