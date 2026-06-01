namespace Assembler.Resolving.Behaviours
{
	public sealed class SetActiveData : BehaviourData
	{
		public IValueProvider<bool> Active { get; }

		public SetActiveData(string id, IValueProvider<bool> active) :
			base(id) => Active = active;
	}
}
