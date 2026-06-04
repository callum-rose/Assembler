using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class SpeedLimitData : BehaviourData
	{
		public IValueProvider<Vector3> Velocity { get; }
		public IValueProvider<float> Max { get; }

		public SpeedLimitData(string id,
			IValueProvider<Vector3> velocity,
			IValueProvider<float> max) :
			base(id) => (Velocity, Max) = (velocity, max);
	}
}
