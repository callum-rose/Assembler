using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public class UICanvasData : BehaviourData
	{
		public IValueProvider<float> MatchWidthOrHeight { get; }
		public IValueProvider<Vector3> ReferenceResolution { get; }

		public UICanvasData(string id,
			IValueProvider<float> matchWidthOrHeight,
			IValueProvider<Vector3> referenceResolution) : base(id) =>
			(MatchWidthOrHeight, ReferenceResolution) = (matchWidthOrHeight, referenceResolution);
	}
}
