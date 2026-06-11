
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers.Timing
{
	/// <summary>Fires every Unity Update frame. Use for behaviours that must run continuously.</summary>
	/// <remarks>
	/// Properties:
	/// </remarks>
	public class EveryFrameTrigger : TimingTrigger<EveryFrameTriggerData>
	{
		private void Update()
		{
			NotifyListeners(TriggerContext.Empty);
		}
	}
}
