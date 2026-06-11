namespace Assembler.Resolving.Behaviours
{
	public sealed class ToggleBehaviourEnabledData : BehaviourData
	{
		public BehaviourTargets Targets { get; }

		public ToggleBehaviourEnabledData(string id, BehaviourTargets targets) :
			base(id) => Targets = targets;
	}
}
