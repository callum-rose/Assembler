using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class FormatStringSetterData : BehaviourData
	{
		public IValueProvider<string> ValueToSet { get; }
		public IValueProvider<string> Format { get; }
		public IReadOnlyList<IValueProvider> Arguments { get; }

		public FormatStringSetterData(string id,
			IReadOnlyList<Action> listeners,
			IValueProvider<string> valueToSet,
			IValueProvider<string> format,
			IReadOnlyList<IValueProvider> arguments) : base(id, listeners) =>
			(ValueToSet, Format, Arguments) = (valueToSet, format, arguments);
	}
}
