using System.Collections.Generic;


namespace Assembler.Parsing.Info.Behaviours
{
	public record UIToggleInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<bool> InitialValue,
		ValueSource<string> Label,
		ScreenRect Rect) : BehaviourInfo(Id, Listeners)
	{
		public static UIToggleInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource(ctx, props.GetValueOrDefault("InitialValue"), fallback: false),
				Transformer.CreateValueSource(ctx, props.GetValueOrDefault("Label"), fallback: string.Empty),
				ScreenRectParser.Parse(props.GetValueOrDefault("Rect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new UIToggleInfo(Id,
				substitutedListeners,
				InitialValue.SubstituteParameters(ctx),
				Label.SubstituteParameters(ctx),
				Rect);
	}
}
