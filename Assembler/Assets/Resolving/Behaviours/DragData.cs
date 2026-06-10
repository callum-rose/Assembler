using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class DragData : BehaviourData
	{
		public IWriteValueProvider<Vector3> Velocity { get; }
		public IValueProvider<float> Coefficient { get; }

		public DragData(string id,
			IWriteValueProvider<Vector3> velocity,
			IValueProvider<float> coefficient) :
			base(id) => (Velocity, Coefficient) = (velocity, coefficient);
	}
}
