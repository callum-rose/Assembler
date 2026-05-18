using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class TapTriggerData : TriggerData
	{
		public TapTriggerData(string id, IReadOnlyList<Action> listeners) : base(id, listeners) { }
	}
}