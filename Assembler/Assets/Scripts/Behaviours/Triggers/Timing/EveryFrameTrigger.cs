using Assembler.Parsing.Phase2.Parsing.Phase2.Info;

namespace AssemblerAlpha.Behaviours.Triggers.Timing
{
	public class EveryFrameTrigger : TimingTrigger<EveryFrameInfo>
	{
		protected override void OnInitialise(EveryFrameInfo behaviourInfo)
		{
		}

		private void Update()
		{
			InvokeTrigger();
		}
	}
}