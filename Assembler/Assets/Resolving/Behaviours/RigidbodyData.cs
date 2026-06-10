using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class RigidbodyData : BehaviourData
	{
		public IValueProvider<bool> IsKinematic { get; init; } = NullValueProvider<bool>.Instance;
		public IValueProvider<bool> UseGravity { get; init; } = NullValueProvider<bool>.Instance;
		public IValueProvider<float> Mass { get; init; } = NullValueProvider<float>.Instance;
		public IValueProvider<float> LinearDamping { get; init; } = NullValueProvider<float>.Instance;
		public IValueProvider<float> AngularDamping { get; init; } = NullValueProvider<float>.Instance;
		public IValueProvider<Vector3> FreezePosition { get; init; } = NullValueProvider<Vector3>.Instance;
		public IValueProvider<Vector3> FreezeRotation { get; init; } = NullValueProvider<Vector3>.Instance;
		public IValueProvider<Vector3> CentreOfMass { get; init; } = NullValueProvider<Vector3>.Instance;

		public RigidbodyData(string id) : base(id) { }
	}
}
