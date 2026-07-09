using Assembler.Parsing.Info.Behaviours;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class PointerTriggerData : TriggerData
	{
		public IValueProvider<PointerPhase> Phase { get; }
		public IValueProvider<Vector3> PlanePoint { get; }
		public IValueProvider<Vector3> PlaneNormal { get; }

		public PointerTriggerData(string id,
			IValueProvider<PointerPhase> phase,
			IValueProvider<Vector3> planePoint,
			IValueProvider<Vector3> planeNormal) :
			base(id) => (Phase, PlanePoint, PlaneNormal) = (phase, planePoint, planeNormal);
	}
}
