using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public class ListRemoveAtData<T> : BehaviourData
	{
		public IValueProvider<List<T>> List { get; }
		public IValueProvider<int> Index { get; }

		public ListRemoveAtData(string id,
						IValueProvider<List<T>> list,
			IValueProvider<int> index) : base(id) =>
			(List, Index) = (list, index);
	}
}
