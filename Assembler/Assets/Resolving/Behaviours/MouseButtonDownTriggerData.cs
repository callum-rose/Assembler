namespace Assembler.Resolving.Behaviours
{
	public sealed class MouseButtonDownTriggerData : TriggerData
	{
		public IValueProvider<int> Button { get; }

		public MouseButtonDownTriggerData(string id, IValueProvider<int> button) :
			base(id) => Button = button;
	}
}
