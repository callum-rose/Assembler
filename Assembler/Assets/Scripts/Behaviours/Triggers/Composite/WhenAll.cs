using System.Collections.Generic;
using Assembler.Generators.Attributes;
using Core;

namespace Behaviours.Triggers.Composite
{
	public class WhenAll : Trigger
	{
		[Inject("Triggers")] private Trigger[] _triggers;

		private readonly List<Trigger> _triggeredTriggers = new();

		protected override void OnInitialise()
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