using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ProgressBarInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<float> Value,
		ScreenRect Rect) : BehaviourInfo(Id, Listeners)
	{
		public static ProgressBarInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<float>(v, props.GetValueOrDefault("Value"), parameters: p),
				ScreenRectParser.Parse(props.GetValueOrDefault("Rect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new ProgressBarInfo(Id,
				substitutedListeners,
				Value.SubstituteParameters(parameters, allValues),
				Rect);
	}
}
