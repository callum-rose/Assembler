using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class CameraOrbitData : BehaviourData
	{
		public IValueProvider<Transform> Follow { get; }
		public IValueProvider<float> Radius { get; }
		public IValueProvider<float> Height { get; }
		public IValueProvider<float> Damping { get; }
		public IValueProvider<int> Priority { get; }
		public IValueProvider<float> Lens { get; }

		public CameraOrbitData(string id,
			IValueProvider<Transform> follow,
			IValueProvider<float> radius,
			IValueProvider<float> height,
			IValueProvider<float> damping,
			IValueProvider<int> priority,
			IValueProvider<float> lens) : base(id) =>
			(Follow, Radius, Height, Damping, Priority, Lens) = (follow, radius, height, damping, priority, lens);
	}
}
