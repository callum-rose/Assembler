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
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<bool>(v, props?.GetValueOrDefault("InitialValue"), fallback: false, parameters: p),
				Transformer.CreateValueSource<string>(v, props?.GetValueOrDefault("Label"), fallback: string.Empty, parameters: p),
				ScreenRectParser.Parse(props?.GetValueOrDefault("Rect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new UIToggleInfo(Id,
				substitutedListeners,
				InitialValue.SubstituteParameters(parameters, allValues),
				Label.SubstituteParameters(parameters, allValues),
				Rect);
	}
}
