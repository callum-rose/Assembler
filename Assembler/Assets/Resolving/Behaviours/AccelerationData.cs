using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class AccelerationData : BehaviourData
	{
		public IValueProvider<Vector3> Acceleration { get; }
		public IWriteValueProvider<Vector3> Velocity { get; }

		public AccelerationData(string id,
			IValueProvider<Vector3> acceleration,
			IWriteValueProvider<Vector3> velocity) :
			base(id) => (Acceleration, Velocity) = (acceleration, velocity);
	}
}
