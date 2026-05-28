using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public class ListRemoveData<T> : BehaviourData
	{
		public IValueProvider<List<T>> List { get; }
		public IValueProvider<T> Value { get; }

		public ListRemoveData(string id,
			IValueProvider<List<T>> list,
			IValueProvider<T> value) : base(id) =>
			(List, Value) = (list, value);
	}
}
