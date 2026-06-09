using Assembler.Parsing.Info.Behaviours;

namespace Assembler.Resolving.Behaviours
{
	public class CameraData : BehaviourData
	{
		public IValueProvider<CameraProjection> View { get; }
		public IValueProvider<float> Size { get; }
		public IValueProvider<float> DefaultBlend { get; }

		public CameraData(string id,
			IValueProvider<CameraProjection> view,
			IValueProvider<float> size,
			IValueProvider<float> defaultBlend) : base(id) =>
			(View, Size, DefaultBlend) = (view, size, defaultBlend);
	}
}
