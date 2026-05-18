using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class RotateTriggerData : TriggerData
	{
		public RotateTriggerData(string id, IReadOnlyList<Action> listeners) : base(id, listeners) { }
	}
}