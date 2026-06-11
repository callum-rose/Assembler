using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers.Timing
{
	/// <summary>Fires every Unity FixedUpdate step. Use for physics-step-aligned, fixed-timestep logic.</summary>
	/// <remarks>
	/// Properties:
	/// </remarks>
	public class FixedUpdateTrigger : TimingTrigger<FixedUpdateTriggerData>
	{
		private void FixedUpdate()
		{
			NotifyListeners(TriggerContext.Empty);
		}
	}
}
