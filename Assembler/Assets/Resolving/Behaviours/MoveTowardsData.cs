using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class MoveTowardsData : BehaviourData
	{
		public IValueProvider<Vector3> Target { get; }
		public IValueProvider<float> Speed { get; }

		public MoveTowardsData(string id,
			IValueProvider<Vector3> target,
			IValueProvider<float> speed) :
			base(id) => (Target, Speed) = (target, speed);
	}
}
