using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record PrimitiveInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<string> Shape,
		ValueSource<Color> Colour,
		ValueSource<Vector3> Size)
		: BehaviourInfo(Id, Listeners)
	{
		public static PrimitiveInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<string>(ctx, props.GetValueOrDefault("Shape")),
				Transformer.CreateValueSource<Color>(ctx, props.GetValueOrDefault("Colour")),
				Transformer.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Size")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new PrimitiveInfo(Id,
				substitutedListeners,
				Shape.SubstituteParameters(ctx),
				Colour.SubstituteParameters(ctx),
				Size.SubstituteParameters(ctx));
	}
}
