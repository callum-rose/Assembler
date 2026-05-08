using System.Collections.Generic;
using Core;

namespace Behaviours.Triggers.Composite
{
	public class WhenAll : Trigger
	{
		private Trigger[] _triggers;

		private readonly List<Trigger> _triggeredTriggers = new();

		protected override void OnInitialise(Configuration configuration)
		{
			foreach (var trigger in _triggers)
			{
				trigger.Triggered += () => TriggerOnTriggered(trigger);
			}
		}

		public override void Execute() { }

		private void TriggerOnTriggered(Trigger trigger)
		{
			_triggeredTriggers.Add(trigger);

			if (_triggeredTriggers.Count == _triggers.Length)
			{
				InvokeTrigger();
			}
		}
	}
}