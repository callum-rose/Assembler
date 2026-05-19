using System.Collections.Generic;
using Assembler.Resolving;

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
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<float>(v, props?.GetValueOrDefault("InitialValue"), fallback: 0f, parameters: p),
				Transformer.CreateValueSource<float>(v, props?.GetValueOrDefault("MinValue"), fallback: 0f, parameters: p),
				Transformer.CreateValueSource<float>(v, props?.GetValueOrDefault("MaxValue"), fallback: 1f, parameters: p),
				ScreenRectParser.Parse(props?.GetValueOrDefault("Rect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new UISliderInfo(Id,
				substitutedListeners,
				InitialValue.SubstituteParameters(parameters, allValues),
				MinValue.SubstituteParameters(parameters, allValues),
				MaxValue.SubstituteParameters(parameters, allValues),
				Rect);
	}
}
