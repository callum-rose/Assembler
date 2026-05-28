using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class AddTorqueData : BehaviourData
	{
		public IValueProvider<Vector3> Torque { get; }

		public AddTorqueData(string id, IValueProvider<Vector3> torque) : base(id) => Torque = torque;
	}
}
