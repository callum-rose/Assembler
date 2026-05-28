using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class AddImpulseData : BehaviourData
	{
		public IValueProvider<Vector3> Impulse { get; }

		public AddImpulseData(string id, IValueProvider<Vector3> impulse) : base(id) => Impulse = impulse;
	}
}
