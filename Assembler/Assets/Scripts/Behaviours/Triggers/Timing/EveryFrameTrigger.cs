namespace Behaviours.Triggers.Timing
{
	public class EveryFrameTrigger : TimingTrigger
	{
		private void Update()
		{
			InvokeTrigger();
		}
	}
}