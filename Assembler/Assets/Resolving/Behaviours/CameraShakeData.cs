using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class CameraShakeData : BehaviourData
	{
		public IValueProvider<float> Force { get; }
		public IValueProvider<float> Duration { get; }
		public IValueProvider<Vector3> Velocity { get; }

		public CameraShakeData(string id,
			IValueProvider<float> force,
			IValueProvider<float> duration,
			IValueProvider<Vector3> velocity) : base(id) =>
			(Force, Duration, Velocity) = (force, duration, velocity);
	}
}
