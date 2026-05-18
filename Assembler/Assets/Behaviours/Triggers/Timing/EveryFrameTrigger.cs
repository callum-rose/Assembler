
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers.Timing
{
	public class EveryFrameTrigger : TimingTrigger<EveryFrameTriggerData>
	{
		private void Update()
		{
			InvokeListeners();
		}
	}
}