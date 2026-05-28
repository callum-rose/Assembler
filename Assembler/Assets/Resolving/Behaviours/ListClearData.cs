using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public class ListClearData<T> : BehaviourData
	{
		public IValueProvider<List<T>> List { get; }

		public ListClearData(string id,
						IValueProvider<List<T>> list) : base(id) =>
			List = list;
	}
}
