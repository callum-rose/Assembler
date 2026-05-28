using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public class ListSetAtData<T> : BehaviourData
	{
		public IValueProvider<List<T>> List { get; }
		public IValueProvider<int> Index { get; }
		public IValueProvider<T> Value { get; }

		public ListSetAtData(string id,
						IValueProvider<List<T>> list,
			IValueProvider<int> index,
			IValueProvider<T> value) : base(id) =>
			(List, Index, Value) = (list, index, value);
	}
}
