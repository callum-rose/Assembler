using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class LongPressTriggerData : TriggerData
	{
		public LongPressTriggerData(string id, IReadOnlyList<Action> listeners) : base(id, listeners) { }
	}
}