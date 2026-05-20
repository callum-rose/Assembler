using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public class ListRemoveAtData<T> : BehaviourData
	{
		public IValueProvider<IList<T>> List { get; }
		public IValueProvider<int> Index { get; }

		public ListRemoveAtData(string id,
			IReadOnlyList<Action> listeners,
			IValueProvider<IList<T>> list,
			IValueProvider<int> index) : base(id, listeners) =>
			(List, Index) = (list, index);
	}
}
