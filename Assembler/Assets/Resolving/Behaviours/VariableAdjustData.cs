using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public class VariableAdjustData<T> : BehaviourData
	{
		public IValueProvider<T> ValueToSet { get; }
		public IValueProvider<T> Delta { get; }

		public VariableAdjustData(string id,
			IReadOnlyList<Action> listeners,
			IValueProvider<T> valueToSet,
			IValueProvider<T> delta) : base(id, listeners) =>
			(ValueToSet, Delta) = (valueToSet, delta);
	}
}
