using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class SetVelocityData : BehaviourData
	{
		public IValueProvider<Vector3> Velocity { get; }

		public SetVelocityData(string id, IValueProvider<Vector3> velocity) : base(id) => Velocity = velocity;
	}
}
