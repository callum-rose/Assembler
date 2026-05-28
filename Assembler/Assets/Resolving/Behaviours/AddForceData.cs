using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class AddForceData : BehaviourData
	{
		public IValueProvider<Vector3> Force { get; }

		public AddForceData(string id, IValueProvider<Vector3> force) : base(id) => Force = force;
	}
}
