namespace Assembler.Resolving.Behaviours
{
	public sealed class AxisTriggerData : TriggerData
	{
		public IValueProvider<string> XAxis { get; }
		public IValueProvider<string> YAxis { get; }

		public AxisTriggerData(string id, IValueProvider<string> xAxis, IValueProvider<string> yAxis) :
			base(id)
		{
			XAxis = xAxis;
			YAxis = yAxis;
		}
	}
}
