namespace Assembler.Resolving.Behaviours
{
	public sealed class GamepadButtonTriggerData : TriggerData
	{
		public IValueProvider<string> Button { get; }
		public IValueProvider<string> Mode { get; }

		public GamepadButtonTriggerData(string id, IValueProvider<string> button, IValueProvider<string> mode) :
			base(id)
		{
			Button = button;
			Mode = mode;
		}
	}
}
