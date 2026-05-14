
using Assembler.Resolving;

namespace Assembler.Behaviours.Triggers.Timing
{
	public class OnStartTrigger : TimingTrigger<OnStartTriggerData>
	{
		private void Start()
		{
			InvokeListeners();
		}
	}
}