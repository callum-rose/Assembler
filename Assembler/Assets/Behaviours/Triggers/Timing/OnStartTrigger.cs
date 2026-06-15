
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

		// Unity's once-per-lifetime Start does not re-run on a reused component, so re-fire the "on start" event
		// when a pooled entity is respawned — its listeners are re-resolved by the time OnReuse runs.
		public override void OnReuse() => NotifyListeners(TriggerContext.Empty);
	}
}
