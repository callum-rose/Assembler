using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class SpeedLimitData : BehaviourData
	{
		public IWriteValueProvider<Vector3> Velocity { get; }
		public IValueProvider<float> Max { get; }

		public SpeedLimitData(string id,
			IWriteValueProvider<Vector3> velocity,
			IValueProvider<float> max) :
			base(id) => (Velocity, Max) = (velocity, max);
	}
}
