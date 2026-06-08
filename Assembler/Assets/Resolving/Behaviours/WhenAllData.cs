using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class WhenAllData : BehaviourData
	{
		public IReadOnlyList<string> TriggerIds { get; }

		public WhenAllData(string id, IReadOnlyList<string> triggerIds) :
			base(id) => TriggerIds = triggerIds;
	}
}
