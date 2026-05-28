namespace Assembler.Resolving.Behaviours
{
	public sealed class DebouncedTriggerData : TriggerData
	{
		public IValueProvider<float> Interval { get; }

		public DebouncedTriggerData(string id, IValueProvider<float> interval) :
			base(id) => Interval = interval;
	}
}
