using System.Collections.Generic;
using Assembler.Generators.Attributes;
using Core;

namespace Behaviours.Triggers.Composite
{
	public partial class WhenAny : Trigger
	{
		[Inject("Triggers")] private IReadOnlyList<Trigger> _triggers;

		protected override void OnInitialise()
		{
			foreach (var trigger in _triggers)
			{
				trigger.Triggered += InvokeTrigger;
			}
		}

		public override void Execute() { }
	}
}