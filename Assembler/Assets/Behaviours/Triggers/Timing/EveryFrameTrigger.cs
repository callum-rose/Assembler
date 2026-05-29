
using System;
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
		public override void Execute(TriggerContext ctx)
		{
			throw new Exception($"Cannot execute an {nameof(EveryFrameTrigger)} manually");
		}

		private void Update()
		{
			NotifyListeners(TriggerContext.Empty);
		}
	}
}