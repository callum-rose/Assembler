
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers.Timing
{
	/// <summary>Fires once when the entity is first started.</summary>
	/// <remarks>
	/// Properties:
	/// </remarks>
	public class OnStartTrigger : TimingTrigger<OnStartTriggerData>
	{
		private void Start()
		{
			NotifyListeners(TriggerContext.Empty);
		}
	}
}
