namespace Assembler.Resolving.Behaviours
{
	public sealed class MouseButtonHoldTriggerData : TriggerData
	{
		public IValueProvider<int> Button { get; }

		public MouseButtonHoldTriggerData(string id, IValueProvider<int> button) :
			base(id) => Button = button;
	}
}
