
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

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