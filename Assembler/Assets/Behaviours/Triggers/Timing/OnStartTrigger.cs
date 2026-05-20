
using System;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers.Timing
{
	public class OnStartTrigger : TimingTrigger<OnStartTriggerData>
	{
		private bool _started;

		public override void Execute()
		{
			throw new Exception($"{nameof(OnStartTrigger)} cannot be executed directly.");
		}

		private void Start()
		{
			if (_started) return;
			_started = true;
			NotifyListeners();
		}

		public override void OnPostInitialise()
		{
			if (_started) return;
			_started = true;
			NotifyListeners();
		}

		public override void OnDespawn()
		{
			_started = false;
		}
	}
}