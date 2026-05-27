using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public class ListLengthData<T> : BehaviourData
	{
		public IValueProvider<IList<T>> List { get; }
		public IValueProvider<int> Length { get; }

		public ListLengthData(string id,
			IReadOnlyList<Action> listeners,
			IValueProvider<IList<T>> list,
			IValueProvider<int> length) : base(id, listeners) =>
			(List, Length) = (list, length);
	}
}
