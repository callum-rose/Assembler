using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class CollisionStayTriggerData : PhysicalTriggerData
	{
		public CollisionStayTriggerData(string id, IReadOnlyList<string> tags) :
			base(id, tags)
		{ }
	}
}
