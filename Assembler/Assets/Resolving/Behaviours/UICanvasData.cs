namespace Assembler.Resolving.Behaviours
{
	public class UICanvasData : BehaviourData
	{
		public IValueProvider<float> MatchWidthOrHeight { get; }

		public UICanvasData(string id,
			IValueProvider<float> matchWidthOrHeight) : base(id) =>
			MatchWidthOrHeight = matchWidthOrHeight;
	}
}
