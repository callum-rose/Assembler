using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class LookAtData : BehaviourData
	{
		public IValueProvider<Vector3> Target { get; }
		public IValueProvider<float> TurnRate { get; }

		public LookAtData(string id,
			IValueProvider<Vector3> target,
			IValueProvider<float> turnRate) :
			base(id) => (Target, TurnRate) = (target, turnRate);
	}
}
