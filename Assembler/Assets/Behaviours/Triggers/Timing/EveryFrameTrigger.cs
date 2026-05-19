
using System;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers.Timing
{
	public class EveryFrameTrigger : TimingTrigger<EveryFrameTriggerData>
	{
		public override void Execute()
		{
			throw new Exception($"Cannot execute an {nameof(EveryFrameTrigger)} manually");
		}
		
		private void Update()
		{
			NotifyListeners();
		}
	}
}