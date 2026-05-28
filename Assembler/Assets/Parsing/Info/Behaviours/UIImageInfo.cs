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
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Color>(ctx, props.GetValueOrDefault("Colour")),
				ScreenRectParser.Parse(props.GetValueOrDefault("Rect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new UIImageInfo(Id,
				substitutedListeners,
				Colour.SubstituteParameters(ctx),
				Rect);
	}
}
