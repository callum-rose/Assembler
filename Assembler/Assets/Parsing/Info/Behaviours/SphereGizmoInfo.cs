using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record SphereGizmoInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<float> Radius,
		ValueSource<bool> IsWire,
		ValueSource<Color> Colour) : BehaviourInfo(Id, Listeners)
	{
		public static SphereGizmoInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("Radius")),
				Transformer.CreateValueSource<bool>(ctx, props.GetValueOrDefault("IsWire")),
				Transformer.CreateValueSource<Color>(ctx, props.GetValueOrDefault("Colour")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new SphereGizmoInfo(Id,
				substitutedListeners,
				Radius.SubstituteParameters(ctx),
				IsWire.SubstituteParameters(ctx),
				Colour.SubstituteParameters(ctx));
	}
}