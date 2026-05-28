namespace Assembler.Resolving.Behaviours
{
	public sealed class MouseButtonUpTriggerData : TriggerData
	{
		public IValueProvider<int> Button { get; }

		public MouseButtonUpTriggerData(string id, IValueProvider<int> button) :
			base(id) => Button = button;
	}
}
