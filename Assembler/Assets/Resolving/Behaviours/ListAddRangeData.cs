using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public class ListAddRangeData<T> : BehaviourData
	{
		public IValueProvider<List<T>> List { get; }
		public IValueProvider<List<T>> Other { get; }

		public ListAddRangeData(string id,
			IValueProvider<List<T>> list,
			IValueProvider<List<T>> other) : base(id) =>
			(List, Other) = (list, other);
	}
}
