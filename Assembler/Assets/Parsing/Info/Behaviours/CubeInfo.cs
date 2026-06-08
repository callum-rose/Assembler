using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record CubeInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Color> Colour,
		ValueSource<Vector3> Size)
		: BehaviourInfo(Id, Listeners)
	{
		public static CubeInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Color>(ctx, props.GetValueOrDefault("Colour")),
				Transformer.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Size")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new CubeInfo(Id,
				substitutedListeners,
				Colour.SubstituteParameters(ctx),
				Size.SubstituteParameters(ctx));
	}
}
