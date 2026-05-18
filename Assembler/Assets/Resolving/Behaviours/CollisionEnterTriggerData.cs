using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class CollisionEnterTriggerData : PhysicalTriggerData
	{
		public CollisionEnterTriggerData(string id, IReadOnlyList<string> tags, IReadOnlyList<Action> listeners) :
			base(id, tags, listeners) { }
	}
}