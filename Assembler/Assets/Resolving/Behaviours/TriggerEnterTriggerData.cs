using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class TriggerEnterTriggerData : PhysicalTriggerData
	{
		public TriggerEnterTriggerData(string id, IReadOnlyList<string> tags, IReadOnlyList<Action> listeners) :
			base(id, tags, listeners) { }
	}
}