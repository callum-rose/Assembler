using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class SmoothMoveData : BehaviourData
	{
		public IValueProvider<Vector3> Target { get; }
		public IValueProvider<float> SmoothTime { get; }

		public SmoothMoveData(string id,
			IValueProvider<Vector3> target,
			IValueProvider<float> smoothTime) :
			base(id) => (Target, SmoothTime) = (target, smoothTime);
	}
}
