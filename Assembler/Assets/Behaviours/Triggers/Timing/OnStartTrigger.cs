
using System;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers.Timing
{
	public class OnStartTrigger : TimingTrigger<OnStartTriggerData>
	{
		public override void Execute()
		{
			throw new Exception($"{nameof(OnStartTrigger)} cannot be executed directly.");
		}

		private void Start()
		{
			NotifyListeners();
		}
	}
}