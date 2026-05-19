using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class ForEachTriggerData : TriggerData
	{
		public IValueProvider<IReadOnlyList<string>> Entities { get; }

		public ForEachTriggerData(
			string id,
			IReadOnlyList<Action> listeners,
			IValueProvider<IReadOnlyList<string>> entities)
			: base(id, listeners)
		{
			Entities = entities;
		}
	}
}
