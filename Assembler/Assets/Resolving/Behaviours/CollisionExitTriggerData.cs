using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class CollisionExitTriggerData : PhysicalTriggerData
	{
		public CollisionExitTriggerData(string id, IReadOnlyList<string> tags, IReadOnlyList<Action> listeners) :
			base(id, tags, listeners) { }
	}
}