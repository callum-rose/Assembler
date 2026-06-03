namespace Assembler.Resolving.Behaviours
{
	public class UIContainerData : BehaviourData
	{
		public IValueProvider<string> Direction { get; }
		public IValueProvider<float> Spacing { get; }
		public IValueProvider<float> Padding { get; }
		public IValueProvider<string> ChildAlignment { get; }
		public IValueProvider<bool> FitContent { get; }

		public UIContainerData(string id,
			IValueProvider<string> direction,
			IValueProvider<float> spacing,
			IValueProvider<float> padding,
			IValueProvider<string> childAlignment,
			IValueProvider<bool> fitContent) : base(id) =>
			(Direction, Spacing, Padding, ChildAlignment, FitContent) =
				(direction, spacing, padding, childAlignment, fitContent);
	}
}
