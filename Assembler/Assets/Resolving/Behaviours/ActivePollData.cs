namespace Assembler.Resolving.Behaviours
{
	public sealed class ActivePollData : BehaviourData
	{
		public IValueProvider<bool> Active { get; }

		public ActivePollData(string id, IValueProvider<bool> active) :
			base(id) => Active = active;
	}
}
