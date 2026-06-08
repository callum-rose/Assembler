using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class WrapPositionData : BehaviourData
	{
		public IValueProvider<Vector3> Min { get; }
		public IValueProvider<Vector3> Max { get; }

		public WrapPositionData(string id,
			IValueProvider<Vector3> min,
			IValueProvider<Vector3> max) :
			base(id) => (Min, Max) = (min, max);
	}
}
