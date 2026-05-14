
using Assembler.Resolving;

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