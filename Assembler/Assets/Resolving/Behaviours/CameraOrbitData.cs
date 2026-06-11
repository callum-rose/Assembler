using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class CameraOrbitData : BehaviourData
	{
		public IValueProvider<Transform> Follow { get; }
		public IValueProvider<float> Radius { get; }
		public IValueProvider<float> Height { get; }
		public IValueProvider<float> OrbitSpeed { get; }
		public IValueProvider<float> Damping { get; }
		public IValueProvider<int> Priority { get; }
		public IValueProvider<float> Lens { get; }

		public CameraOrbitData(string id,
			IValueProvider<Transform> follow,
			IValueProvider<float> radius,
			IValueProvider<float> height,
			IValueProvider<float> orbitSpeed,
			IValueProvider<float> damping,
			IValueProvider<int> priority,
			IValueProvider<float> lens) : base(id) =>
			(Follow, Radius, Height, OrbitSpeed, Damping, Priority, Lens) =
			(follow, radius, height, orbitSpeed, damping, priority, lens);
	}
}
