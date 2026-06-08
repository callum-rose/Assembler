using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record PlaneInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Color> Colour,
		ValueSource<Vector3> Size)
		: BehaviourInfo(Id, Listeners)
	{
		public static PlaneInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Color>(ctx, props.GetValueOrDefault("Colour")),
				Transformer.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Size")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new PlaneInfo(Id,
				substitutedListeners,
				Colour.SubstituteParameters(ctx),
				Size.SubstituteParameters(ctx));
	}
}
