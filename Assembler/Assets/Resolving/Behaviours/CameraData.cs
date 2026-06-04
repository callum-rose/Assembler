namespace Assembler.Resolving.Behaviours
{
	public class CameraData : BehaviourData
	{
		public IValueProvider<string> Perspective { get; }
		public IValueProvider<float> Size { get; }
		public IValueProvider<float> DefaultBlend { get; }

		public CameraData(string id,
			IValueProvider<string> perspective,
			IValueProvider<float> size,
			IValueProvider<float> defaultBlend) : base(id) =>
			(Perspective, Size, DefaultBlend) = (perspective, size, defaultBlend);
	}
}
