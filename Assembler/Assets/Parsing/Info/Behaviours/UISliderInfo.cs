using System.Collections.Generic;


namespace Assembler.Parsing.Info.Behaviours
{
	public record UISliderInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<float> InitialValue,
		ValueSource<float> MinValue,
		ValueSource<float> MaxValue,
		ScreenRect Rect) : BehaviourInfo(Id, Listeners)
	{
		public static UISliderInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource(v, props.GetValueOrDefault("InitialValue"), fallback: 0f, parameters: p),
				Transformer.CreateValueSource(v, props.GetValueOrDefault("MinValue"), fallback: 0f, parameters: p),
				Transformer.CreateValueSource(v, props.GetValueOrDefault("MaxValue"), fallback: 1f, parameters: p),
				ScreenRectParser.Parse(props.GetValueOrDefault("Rect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new UISliderInfo(Id,
				substitutedListeners,
				InitialValue.SubstituteParameters(parameters, allValues),
				MinValue.SubstituteParameters(parameters, allValues),
				MaxValue.SubstituteParameters(parameters, allValues),
				Rect);
	}
}
