namespace Assembler.Resolving.Behaviours
{
	public sealed class LongPressTriggerData : TriggerData
	{
		public IValueProvider<float> Duration { get; }
		public IValueProvider<float> MaxMovement { get; }

		public LongPressTriggerData(string id, IValueProvider<float> duration, IValueProvider<float> maxMovement) :
			base(id)
		{
			Duration = duration;
			MaxMovement = maxMovement;
		}
	}
}
