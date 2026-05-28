using System.Collections.Generic;


namespace Assembler.Parsing.Info.Behaviours
{
	public record TextLabelInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<string> Text,
		ValueSource<string> Label,
		ValueSource<int> FontSize,
		ScreenRect Rect) : BehaviourInfo(Id, Listeners)
	{
		public static TextLabelInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<string>(ctx, props.GetValueOrDefault("Text")),
				Transformer.CreateValueSource(ctx, props.GetValueOrDefault("Label"), fallback: string.Empty),
				Transformer.CreateValueSource(ctx, props.GetValueOrDefault("FontSize"), fallback: 0),
				ScreenRectParser.Parse(props.GetValueOrDefault("Rect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new TextLabelInfo(Id,
				substitutedListeners,
				Text.SubstituteParameters(ctx),
				Label.SubstituteParameters(ctx),
				FontSize.SubstituteParameters(ctx),
				Rect);
	}
}
