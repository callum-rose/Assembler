using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class DeferredTriggerData : TriggerData
	{
		public IValueProvider<float> Delay { get; }

		public DeferredTriggerData(string id, IValueProvider<float> delay) : base(id)
		{
			Delay = delay;
		}
	}
}