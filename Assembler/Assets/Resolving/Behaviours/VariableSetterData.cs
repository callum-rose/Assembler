using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public class VariableSetterData<T> : BehaviourData
	{
		public IValueProvider<T> ValueToSet { get; }
		public IValueProvider<T> ValueToGet { get; }

		public VariableSetterData(string id,
						IValueProvider<T> valueToSet,
			IValueProvider<T> valueToGet) : base(id) =>
			(ValueToSet, ValueToGet) = (valueToSet, valueToGet);
	}
}
