namespace Assembler.Resolving.Behaviours
{
	public sealed class CameraZoomData : BehaviourData
	{
		public IValueProvider<float> Width { get; }
		public IValueProvider<float> Damping { get; }
		public IValueProvider<float> MinFOV { get; }
		public IValueProvider<float> MaxFOV { get; }

		public CameraZoomData(string id,
			IValueProvider<float> width,
			IValueProvider<float> damping,
			IValueProvider<float> minFov,
			IValueProvider<float> maxFov) : base(id) =>
			(Width, Damping, MinFOV, MaxFOV) = (width, damping, minFov, maxFov);
	}
}
