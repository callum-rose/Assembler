using Assembler.Parsing.Phase3;

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