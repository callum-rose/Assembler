using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class ExclusiveTriggerData : TriggerData
	{
		public IValueProvider<string> Group { get; }

		public ExclusiveTriggerData(string id, IValueProvider<string> group) :
			base(id)
		{
			Group = group;
		}
	}
}
