namespace Assembler.Resolving.Behaviours
{
	public sealed class SetBehaviourEnabledData : BehaviourData
	{
		public IValueProvider<bool> Enabled { get; }

		public SetBehaviourEnabledData(string id, IValueProvider<bool> enabled) :
			base(id) => Enabled = enabled;
	}
}
