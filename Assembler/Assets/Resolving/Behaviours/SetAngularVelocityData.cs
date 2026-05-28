using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class SetAngularVelocityData : BehaviourData
	{
		public IValueProvider<Vector3> AngularVelocity { get; }

		public SetAngularVelocityData(string id, IValueProvider<Vector3> angularVelocity) : base(id) =>
			AngularVelocity = angularVelocity;
	}
}
