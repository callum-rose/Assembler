namespace Assembler.Resolving.Behaviours
{
	public sealed class SetBehaviourEnabledData : BehaviourData
	{
		public BehaviourTargets Targets { get; }
		public IValueProvider<bool> Enabled { get; }

		public SetBehaviourEnabledData(string id, BehaviourTargets targets, IValueProvider<bool> enabled) :
			base(id) => (Targets, Enabled) = (targets, enabled);
	}
}
