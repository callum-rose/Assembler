using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class IntervalTriggerData : TriggerData
	{
		public IValueProvider<float> Interval { get; }
		public IValueProvider<int> Count { get; init; }
		public IValueProvider<bool> AutoStart { get; init; }

		public IntervalTriggerData(string id,
			IReadOnlyList<Action> listeners,
			IValueProvider<float> interval,
			IValueProvider<int> count,
			IValueProvider<bool> autoStart) : base(id,
			listeners)
		{
			Interval = interval;
			Count = count;
			AutoStart = autoStart;
		}
	}
}