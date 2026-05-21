using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class ConditionGateData : TriggerData
	{
		public IValueProvider<bool> Condition { get; }

		public ConditionGateData(string id, IValueProvider<bool> condition, IReadOnlyList<Action> listeners) :
			base(id, listeners)
		{
			Condition = condition;
		}
	}
}