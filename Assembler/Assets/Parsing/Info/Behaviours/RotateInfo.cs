using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record RotateInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<Vector3> Displacement)
		: BehaviourInfo(Id, Listeners)
	{
		public static RotateInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Displacement")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new RotateInfo(Id,
				substitutedListeners,
				Displacement.SubstituteParameters(ctx));
	}
}
