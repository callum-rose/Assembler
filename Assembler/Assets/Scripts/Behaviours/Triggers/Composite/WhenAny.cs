using System.Collections.Generic;
using Assembler.Parsing.Phase2.Parsing.Phase2.Info;

namespace AssemblerAlpha.Behaviours.Triggers.Composite
{
	public partial class WhenAny : Trigger<WhenAnyInfo>
	{
		private IReadOnlyList<Trigger<BehaviourInfo>> _triggers;

		protected override void OnInitialise(WhenAnyInfo behaviourInfo)
		{
			foreach (var trigger in _triggers)
			{
				trigger.Triggered += InvokeTrigger;
			}
		}

		public override void Execute() { }
	}
}