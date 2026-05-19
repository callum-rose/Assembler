using System.Collections.Generic;


namespace Assembler.Parsing.Info.Behaviours
{
	public record TextLabelInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<string> Text,
		ValueSource<string> Label,
		ScreenRect Rect) : BehaviourInfo(Id, Listeners)
	{
		public static TextLabelInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<string>(v, props?.GetValueOrDefault("Text"), parameters: p),
				Transformer.CreateValueSource<string>(v, props?.GetValueOrDefault("Label"), fallback: string.Empty, parameters: p),
				ScreenRectParser.Parse(props?.GetValueOrDefault("Rect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new TextLabelInfo(Id,
				substitutedListeners,
				Text.SubstituteParameters(parameters, allValues),
				Label.SubstituteParameters(parameters, allValues),
				Rect);
	}
}
