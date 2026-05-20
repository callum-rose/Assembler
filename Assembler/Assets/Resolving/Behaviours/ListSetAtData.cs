using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public class ListSetAtData<T> : BehaviourData
	{
		public IValueProvider<IList<T>> List { get; }
		public IValueProvider<int> Index { get; }
		public IValueProvider<T> Value { get; }

		public ListSetAtData(string id,
			IReadOnlyList<Action> listeners,
			IValueProvider<IList<T>> list,
			IValueProvider<int> index,
			IValueProvider<T> value) : base(id, listeners) =>
			(List, Index, Value) = (list, index, value);
	}
}
