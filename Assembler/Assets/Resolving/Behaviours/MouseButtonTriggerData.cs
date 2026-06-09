using Assembler.Parsing.Info.Behaviours;

namespace Assembler.Resolving.Behaviours
{
	public sealed class MouseButtonTriggerData : TriggerData
	{
		public IValueProvider<int> Button { get; }
		public IValueProvider<ButtonPhase> Phase { get; }

		public MouseButtonTriggerData(string id, IValueProvider<int> button, IValueProvider<ButtonPhase> phase) :
			base(id)
		{
			Button = button;
			Phase = phase;
		}
	}
}
