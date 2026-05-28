using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public abstract class PhysicalTriggerData : TriggerData
	{
		public IReadOnlyList<string> TagsToDetect { get; }

		protected PhysicalTriggerData(string id,
			IReadOnlyList<string> tagsToDetect) : base(id) => TagsToDetect = tagsToDetect;
	}
}