using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class CameraFollowData : BehaviourData
	{
		public ICameraTarget? Follow { get; }
		public ICameraTarget? LookAt { get; }
		public IValueProvider<int> Priority { get; }
		public IValueProvider<float> Lens { get; }
		public IValueProvider<float> Damping { get; }
		public IValueProvider<float> DeadZone { get; }
		public IValueProvider<float> CameraDistance { get; }
		public IValueProvider<Vector2> ScreenOffset { get; }
		public IValueProvider<Vector3> FollowOffset { get; }

		public CameraFollowData(string id,
			ICameraTarget? follow,
			ICameraTarget? lookAt,
			IValueProvider<int> priority,
			IValueProvider<float> lens,
			IValueProvider<float> damping,
			IValueProvider<float> deadZone,
			IValueProvider<float> cameraDistance,
			IValueProvider<Vector2> screenOffset,
			IValueProvider<Vector3> followOffset) : base(id) =>
			(Follow, LookAt, Priority, Lens, Damping, DeadZone, CameraDistance, ScreenOffset, FollowOffset) =
			(follow, lookAt, priority, lens, damping, deadZone, cameraDistance, screenOffset, followOffset);
	}
}
