
using System;
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
		public override void Execute(TriggerContext ctx)
		{
			throw new Exception($"{nameof(OnStartTrigger)} cannot be executed directly.");
		}

		private void Start()
		{
			NotifyListeners(TriggerContext.Empty);
		}
	}
}