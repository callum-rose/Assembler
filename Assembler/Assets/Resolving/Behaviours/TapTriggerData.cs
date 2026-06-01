namespace Assembler.Resolving.Behaviours
{
	public sealed class TapTriggerData : TriggerData
	{
		public IValueProvider<float> MaxDuration { get; }
		public IValueProvider<float> MaxMovement { get; }

		public TapTriggerData(string id, IValueProvider<float> maxDuration, IValueProvider<float> maxMovement) :
			base(id)
		{
			MaxDuration = maxDuration;
			MaxMovement = maxMovement;
		}
	}
}
