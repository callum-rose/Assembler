using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class ClampPositionData : BehaviourData
	{
		public IValueProvider<Vector3> Min { get; }
		public IValueProvider<Vector3> Max { get; }

		public ClampPositionData(string id,
			IValueProvider<Vector3> min,
			IValueProvider<Vector3> max) :
			base(id) => (Min, Max) = (min, max);
	}
}
