using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public class ListClearData<T> : BehaviourData
	{
		public IValueProvider<IList<T>> List { get; }

		public ListClearData(string id,
						IValueProvider<IList<T>> list) : base(id) =>
			List = list;
	}
}
