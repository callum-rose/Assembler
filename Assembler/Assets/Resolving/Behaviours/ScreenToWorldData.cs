using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class ScreenToWorldData : TriggerData
	{
		public IValueProvider<Vector3> ScreenPosition { get; }
		public IValueProvider<Vector3> PlanePoint { get; }
		public IValueProvider<Vector3> PlaneNormal { get; }

		public ScreenToWorldData(string id,
			IValueProvider<Vector3> screenPosition,
			IValueProvider<Vector3> planePoint,
			IValueProvider<Vector3> planeNormal) :
			base(id) => (ScreenPosition, PlanePoint, PlaneNormal) = (screenPosition, planePoint, planeNormal);
	}
}
