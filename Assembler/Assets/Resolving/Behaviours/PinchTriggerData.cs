using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class PinchTriggerData : TriggerData
	{
		public PinchTriggerData(string id, IReadOnlyList<Action> listeners) : base(id, listeners) { }
	}
}