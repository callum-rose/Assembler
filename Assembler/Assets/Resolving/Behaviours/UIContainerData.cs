using Assembler.Parsing.Info.Behaviours;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public class UIContainerData : BehaviourData
	{
		public IValueProvider<LayoutDirection> Direction { get; }
		public IValueProvider<float> Spacing { get; }
		public IValueProvider<float> Padding { get; }
		public IValueProvider<TextAnchor> ChildAlignment { get; }
		public IValueProvider<bool> FitContent { get; }

		public UIContainerData(string id,
			IValueProvider<LayoutDirection> direction,
			IValueProvider<float> spacing,
			IValueProvider<float> padding,
			IValueProvider<TextAnchor> childAlignment,
			IValueProvider<bool> fitContent) : base(id) =>
			(Direction, Spacing, Padding, ChildAlignment, FitContent) =
				(direction, spacing, padding, childAlignment, fitContent);
	}
}
