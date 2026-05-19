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
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<string>(v, props?.GetValueOrDefault("Label"), fallback: string.Empty, parameters: p),
				ScreenRectParser.Parse(props?.GetValueOrDefault("Rect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new UIButtonInfo(Id,
				substitutedListeners,
				Label.SubstituteParameters(parameters, allValues),
				Rect);
	}
}
