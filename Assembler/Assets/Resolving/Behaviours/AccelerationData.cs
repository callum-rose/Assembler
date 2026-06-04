using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class AccelerationData : BehaviourData
	{
		public IValueProvider<Vector3> Acceleration { get; }
		public IValueProvider<Vector3> Velocity { get; }

		public AccelerationData(string id,
			IValueProvider<Vector3> acceleration,
			IValueProvider<Vector3> velocity) :
			base(id) => (Acceleration, Velocity) = (acceleration, velocity);
	}
}
