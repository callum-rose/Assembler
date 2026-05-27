using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public class ListFillData<T> : BehaviourData
	{
		public IValueProvider<IList<T>> List { get; }
		public IValueProvider<int> Count { get; }
		public IValueProvider<T> Value { get; }

		public ListFillData(string id,
			IReadOnlyList<Action> listeners,
			IValueProvider<IList<T>> list,
			IValueProvider<int> count,
			IValueProvider<T> value) : base(id, listeners) =>
			(List, Count, Value) = (list, count, value);
	}
}
