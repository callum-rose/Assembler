using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public class ListAddData<T> : BehaviourData
	{
		public IValueProvider<IList<T>> List { get; }
		public IValueProvider<T> Value { get; }

		public ListAddData(string id,
			IReadOnlyList<Action> listeners,
			IValueProvider<IList<T>> list,
			IValueProvider<T> value) : base(id, listeners) =>
			(List, Value) = (list, value);
	}
}
