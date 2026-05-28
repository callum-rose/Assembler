using System.Collections.Generic;


namespace Assembler.Parsing.Info.Behaviours
{
	public record UIButtonInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<string> Label,
		ScreenRect Rect) : BehaviourInfo(Id, Listeners)
	{
		public static UIButtonInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource(ctx, props.GetValueOrDefault("Label"), fallback: string.Empty),
				ScreenRectParser.Parse(props.GetValueOrDefault("Rect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new UIButtonInfo(Id,
				substitutedListeners,
				Label.SubstituteParameters(ctx),
				Rect);
	}
}
