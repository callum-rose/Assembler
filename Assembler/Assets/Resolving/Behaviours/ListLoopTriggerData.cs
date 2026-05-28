using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class ListLoopTriggerData<T> : TriggerData
	{
		public IValueProvider<List<T>> List { get; }

		public ListLoopTriggerData(string id, IValueProvider<List<T>> list) : base(id) =>
			List = list;
	}
}
