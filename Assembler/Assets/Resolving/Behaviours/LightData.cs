using Assembler.Parsing.Info.Behaviours;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class LightData : BehaviourData
	{
		public IValueProvider<LightKind> Type { get; }
		public IValueProvider<Color> Colour { get; }
		public IValueProvider<float> Intensity { get; }
		public IValueProvider<float> Range { get; }
		public IValueProvider<float> SpotAngle { get; }

		public LightData(string id,
			IValueProvider<LightKind> type,
			IValueProvider<Color> colour,
			IValueProvider<float> intensity,
			IValueProvider<float> range,
			IValueProvider<float> spotAngle) : base(id) =>
			(Type, Colour, Intensity, Range, SpotAngle) = (type, colour, intensity, range, spotAngle);
	}
}
