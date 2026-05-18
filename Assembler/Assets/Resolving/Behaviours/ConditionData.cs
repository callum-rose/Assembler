using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class ConditionData : TriggerData
	{
		public IValueProvider<bool> Condition { get; }

		public ConditionData(string id, IValueProvider<bool> condition, IReadOnlyList<Action> listeners) :
			base(id, listeners)
		{
			Condition = condition;
		}
	}
}