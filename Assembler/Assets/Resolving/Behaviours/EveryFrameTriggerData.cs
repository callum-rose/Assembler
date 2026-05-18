using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class EveryFrameTriggerData : TriggerData
	{
		public EveryFrameTriggerData(string id, IReadOnlyList<Action> listeners) : base(id, listeners) { }
	}
}