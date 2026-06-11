namespace Assembler.Resolving.Behaviours
{
	public sealed class CameraNoiseData : BehaviourData
	{
		public IValueProvider<string> Profile { get; }
		public IValueProvider<float> Amplitude { get; }
		public IValueProvider<float> Frequency { get; }

		public CameraNoiseData(string id,
			IValueProvider<string> profile,
			IValueProvider<float> amplitude,
			IValueProvider<float> frequency) : base(id) =>
			(Profile, Amplitude, Frequency) = (profile, amplitude, frequency);
	}
}
