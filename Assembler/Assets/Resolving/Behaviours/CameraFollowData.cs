using Assembler.Parsing.Info.Behaviours;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class CameraFollowData : BehaviourData
	{
		public IValueProvider<Transform> Follow { get; }
		public IValueProvider<Transform> LookAt { get; }
		public IValueProvider<CameraFollowMode> Mode { get; }
		public IValueProvider<int> Priority { get; }
		public IValueProvider<float> Lens { get; }
		public IValueProvider<float> Damping { get; }
		public IValueProvider<float> DeadZone { get; }
		public IValueProvider<float> CameraDistance { get; }
		public IValueProvider<Vector3> ScreenOffset { get; }
		public IValueProvider<Vector3> FollowOffset { get; }

		public CameraFollowData(string id,
			IValueProvider<Transform> follow,
			IValueProvider<Transform> lookAt,
			IValueProvider<CameraFollowMode> mode,
			IValueProvider<int> priority,
			IValueProvider<float> lens,
			IValueProvider<float> damping,
			IValueProvider<float> deadZone,
			IValueProvider<float> cameraDistance,
			IValueProvider<Vector3> screenOffset,
			IValueProvider<Vector3> followOffset) : base(id) =>
			(Follow, LookAt, Mode, Priority, Lens, Damping, DeadZone, CameraDistance, ScreenOffset, FollowOffset) =
			(follow, lookAt, mode, priority, lens, damping, deadZone, cameraDistance, screenOffset, followOffset);
	}
}
