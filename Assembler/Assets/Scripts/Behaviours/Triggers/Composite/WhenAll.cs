// using System.Collections.Generic;
// //
// namespace Assembler.Behaviours.Triggers.Composite
// {
// 	public class WhenAll : Trigger<WhenAllInfo>
// 	{
// 		private Trigger<BehaviourInfo>[] _triggers;
//
// 		private readonly List<Trigger<BehaviourInfo>> _triggeredTriggers = new();
//
// 		protected override void OnInitialise(WhenAllInfo behaviourInfo)
// 		{
// 			foreach (var trigger in _triggers)
// 			{
// 				trigger.Triggered += () => TriggerOnTriggered(trigger);
// 			}
// 		}
//
// 		public override void Execute() { }
//
// 		private void TriggerOnTriggered(Trigger<BehaviourInfo> trigger)
// 		{
// 			_triggeredTriggers.Add(trigger);
//
// 			if (_triggeredTriggers.Count == _triggers.Length)
// 			{
// 				InvokeTrigger();
// 			}
// 		}
// 	}
// }