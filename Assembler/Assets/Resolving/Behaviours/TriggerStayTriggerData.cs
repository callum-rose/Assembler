using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class TriggerStayTriggerData : PhysicalTriggerData
	{
		public TriggerStayTriggerData(string id, IReadOnlyList<string> tags) :
			base(id, tags)
		{ }
	}
}
