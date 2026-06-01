using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public class ListSetData<T> : BehaviourData
	{
		public IValueProvider<List<T>> List { get; }
		public IValueProvider<List<T>> Value { get; }

		public ListSetData(string id,
			IValueProvider<List<T>> list,
			IValueProvider<List<T>> value) : base(id) =>
			(List, Value) = (list, value);
	}
}
