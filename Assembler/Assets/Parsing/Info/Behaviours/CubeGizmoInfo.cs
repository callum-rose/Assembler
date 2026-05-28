using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record CubeGizmoInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Vector3> Size,
		ValueSource<bool> IsWire,
		ValueSource<Color> Colour) : BehaviourInfo(Id, Listeners)
	{
		public static CubeGizmoInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Size")),
				Transformer.CreateValueSource<bool>(ctx, props.GetValueOrDefault("IsWire")),
				Transformer.CreateValueSource<Color>(ctx, props.GetValueOrDefault("Colour")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new CubeGizmoInfo(Id,
				substitutedListeners,
				Size.SubstituteParameters(ctx),
				IsWire.SubstituteParameters(ctx),
				Colour.SubstituteParameters(ctx));
	}
}