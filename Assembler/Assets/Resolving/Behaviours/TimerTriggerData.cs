using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class TimerTriggerData : TriggerData
	{
		public IValueProvider<float> Delay { get; }
		public IValueProvider<bool> AutoStart { get; }

		public TimerTriggerData(string id, IValueProvider<float> delay, IValueProvider<bool> autoStart) :
			base(id)
		{
			Delay = delay;
			AutoStart = autoStart;
		}
	}
}
