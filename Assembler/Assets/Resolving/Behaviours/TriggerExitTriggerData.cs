using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class TriggerExitTriggerData : PhysicalTriggerData
	{
		public TriggerExitTriggerData(string id, IReadOnlyList<string> tags) :
			base(id, tags) { }
	}
}