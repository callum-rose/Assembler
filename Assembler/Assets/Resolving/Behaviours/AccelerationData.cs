using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class AccelerationData : BehaviourData
	{
		public IValueProvider<Vector3> Acceleration { get; }

		public AccelerationData(string id, IValueProvider<Vector3> acceleration) :
			base(id) => Acceleration = acceleration;
	}
}
