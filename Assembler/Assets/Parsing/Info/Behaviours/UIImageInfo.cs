using System.Collections.Generic;

using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record UIImageInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Color> Colour,
		ScreenRect Rect) : BehaviourInfo(Id, Listeners)
	{
		public static UIImageInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Color>(v, props.GetValueOrDefault("Colour"), parameters: p),
				ScreenRectParser.Parse(props.GetValueOrDefault("Rect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new UIImageInfo(Id,
				substitutedListeners,
				Colour.SubstituteParameters(parameters, allValues),
				Rect);
	}
}
