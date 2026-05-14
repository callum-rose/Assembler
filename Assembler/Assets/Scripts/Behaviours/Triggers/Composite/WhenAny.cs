// using System.Collections.Generic;
// //
// namespace Assembler.Behaviours.Triggers.Composite
// {
// 	public partial class WhenAny : Trigger<WhenAnyInfo>
// 	{
// 		private IReadOnlyList<Trigger<BehaviourInfo>> _triggers;
//
// 		protected override void OnInitialise(WhenAnyInfo behaviourInfo)
// 		{
// 			foreach (var trigger in _triggers)
// 			{
// 				trigger.Triggered += InvokeTrigger;
// 			}
// 		}
//
// 		public override void Execute() { }
// 	}
// }