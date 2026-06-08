using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record DragInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Vector3> Velocity,
		ValueSource<float> Coefficient)
		: BehaviourInfo(Id, Listeners)
	{
		public static DragInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateOptionalValueSource<Vector3>(ctx, props.GetValueOrDefault("Velocity")),
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("Coefficient")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new DragInfo(Id,
				substitutedListeners,
				Velocity.SubstituteParameters(ctx),
				Coefficient.SubstituteParameters(ctx));
	}
}
