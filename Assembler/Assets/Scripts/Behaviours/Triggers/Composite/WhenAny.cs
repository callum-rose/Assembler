using System.Collections.Generic;
using Core;

namespace Behaviours.Triggers.Composite
{
	public partial class WhenAny : Trigger
	{
		private IReadOnlyList<Trigger> _triggers;

		protected override void OnInitialise(Configuration configuration)
		{
			foreach (var trigger in _triggers)
			{
				trigger.Triggered += InvokeTrigger;
			}
		}

		public override void Execute() { }
	}
}