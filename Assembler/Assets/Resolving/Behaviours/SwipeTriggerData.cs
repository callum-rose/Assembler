using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class SwipeTriggerData : TriggerData
	{
		public SwipeTriggerData(string id, IReadOnlyList<Action> listeners) : base(id, listeners) { }
	}
}