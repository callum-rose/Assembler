namespace Assembler.Resolving.Behaviours
{
	public sealed class SwipeTriggerData : TriggerData
	{
		public IValueProvider<float> MinDistance { get; }
		public IValueProvider<float> MaxDuration { get; }

		public SwipeTriggerData(string id, IValueProvider<float> minDistance, IValueProvider<float> maxDuration) :
			base(id)
		{
			MinDistance = minDistance;
			MaxDuration = maxDuration;
		}
	}
}
