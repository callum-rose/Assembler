namespace Assembler.Resolving.Behaviours
{
	public sealed class BranchData : TriggerData
	{
		public IValueProvider<bool> Condition { get; }

		public BranchData(string id, IValueProvider<bool> condition) :
			base(id)
		{
			Condition = condition;
		}
	}
}
