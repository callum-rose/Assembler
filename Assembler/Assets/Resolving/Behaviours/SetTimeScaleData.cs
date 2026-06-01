namespace Assembler.Resolving.Behaviours
{
	public sealed class SetTimeScaleData : BehaviourData
	{
		public IValueProvider<float> Scale { get; }

		public SetTimeScaleData(string id, IValueProvider<float> scale) :
			base(id) => Scale = scale;
	}
}
