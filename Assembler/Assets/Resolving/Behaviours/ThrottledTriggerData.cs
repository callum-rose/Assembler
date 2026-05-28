namespace Assembler.Resolving.Behaviours
{
	public sealed class ThrottledTriggerData : TriggerData
	{
		public IValueProvider<float> Rate { get; }

		public ThrottledTriggerData(string id, IValueProvider<float> rate) :
			base(id) => Rate = rate;
	}
}
