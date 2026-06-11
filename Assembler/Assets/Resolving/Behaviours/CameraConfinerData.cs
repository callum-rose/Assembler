using Assembler.Parsing.Info.Behaviours;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class CameraConfinerData : BehaviourData
	{
		public IValueProvider<Transform> Bounds { get; }
		public IValueProvider<CameraConfinerMode> Mode { get; }
		public IValueProvider<float> Damping { get; }
		public IValueProvider<float> Padding { get; }

		public CameraConfinerData(string id,
			IValueProvider<Transform> bounds,
			IValueProvider<CameraConfinerMode> mode,
			IValueProvider<float> damping,
			IValueProvider<float> padding) : base(id) =>
			(Bounds, Mode, Damping, Padding) = (bounds, mode, damping, padding);
	}
}
