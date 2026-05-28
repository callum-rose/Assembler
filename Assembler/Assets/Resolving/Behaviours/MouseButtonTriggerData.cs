namespace Assembler.Resolving.Behaviours
{
	public sealed class MouseButtonTriggerData : TriggerData
	{
		public IValueProvider<int> Button { get; }
		public IValueProvider<string> Phase { get; }

		public MouseButtonTriggerData(string id, IValueProvider<int> button, IValueProvider<string> phase) :
			base(id)
		{
			Button = button;
			Phase = phase;
		}
	}
}
