using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class TimerTriggerData : TriggerData
	{
		public IValueProvider<float> Delay { get; }

		public TimerTriggerData(string id, IValueProvider<float> delay, IReadOnlyList<Action> listeners) :
			base(id, listeners) => Delay = delay;
	}
}