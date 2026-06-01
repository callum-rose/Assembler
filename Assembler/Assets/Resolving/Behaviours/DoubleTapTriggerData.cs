namespace Assembler.Resolving.Behaviours
{
	public sealed class DoubleTapTriggerData : TriggerData
	{
		public IValueProvider<float> MaxInterval { get; }
		public IValueProvider<float> MaxMovement { get; }

		public DoubleTapTriggerData(string id, IValueProvider<float> maxInterval, IValueProvider<float> maxMovement) :
			base(id)
		{
			MaxInterval = maxInterval;
			MaxMovement = maxMovement;
		}
	}
}
