using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public abstract class TriggerData : BehaviourData
	{
		protected TriggerData(string id, IReadOnlyList<Action> listeners) : base(id, listeners) { }
	}
}