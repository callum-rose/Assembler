using System.Collections.Generic;


namespace Assembler.Parsing.Info.Behaviours
{
	public record TextLabelInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		AssemblerValue Text,
		IReadOnlyList<ValueInfo> KnownValues,
		ValueSource<string> Label,
		ValueSource<int> FontSize,
		ScreenRect Rect) : BehaviourInfo(Id, Listeners)
	{
		public static TextLabelInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				SubstituteRaw(props.GetValueOrDefault("Text"), p),
				v,
				Transformer.CreateValueSource(v, props.GetValueOrDefault("Label"), fallback: string.Empty, parameters: p),
				Transformer.CreateValueSource(v, props.GetValueOrDefault("FontSize"), fallback: 0, parameters: p),
				ScreenRectParser.Parse(props.GetValueOrDefault("Rect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new TextLabelInfo(Id,
				substitutedListeners,
				SubstituteRaw(Text, parameters),
				allValues,
				Label.SubstituteParameters(parameters, allValues),
				FontSize.SubstituteParameters(parameters, allValues),
				Rect);

		private static AssemblerValue SubstituteRaw(AssemblerValue raw,
			IReadOnlyDictionary<string, AssemblerValue> parameters)
		{
			if (raw is ParamRef paramRef && parameters.TryGetValue(paramRef.Id, out var supplied))
			{
				return supplied;
			}

			return raw;
		}
	}
}
