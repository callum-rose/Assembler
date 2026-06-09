using Assembler.Parsing.Info.Behaviours;

namespace Assembler.Resolving.Behaviours
{
	public sealed class GamepadButtonTriggerData : TriggerData
	{
		public IValueProvider<string> Button { get; }
		public IValueProvider<ButtonPhase> Mode { get; }

		public GamepadButtonTriggerData(string id, IValueProvider<string> button, IValueProvider<ButtonPhase> mode) :
			base(id)
		{
			Button = button;
			Mode = mode;
		}
	}
}
