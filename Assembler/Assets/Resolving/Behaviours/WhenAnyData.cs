using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class WhenAnyData : BehaviourData
	{
		public IReadOnlyList<string> TriggerIds { get; }

		public WhenAnyData(string id, IReadOnlyList<string> triggerIds) :
			base(id) => TriggerIds = triggerIds;
	}
}
