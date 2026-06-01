namespace Assembler.Resolving.Behaviours
{
	public sealed class DragTriggerData : TriggerData
	{
		public IValueProvider<float> Threshold { get; }

		public DragTriggerData(string id, IValueProvider<float> threshold) :
			base(id) => Threshold = threshold;
	}
}
