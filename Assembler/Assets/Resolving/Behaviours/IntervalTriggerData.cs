using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class IntervalTriggerData : TriggerData
	{
		public IValueProvider<float> Interval { get; }
		public IValueProvider<int> Count { get; init; } = new ValueProvider<int>(0);

		public IntervalTriggerData(string id, IValueProvider<float> interval, IReadOnlyList<Action> listeners) : base(id,
			listeners) => Interval = interval;
	}
}