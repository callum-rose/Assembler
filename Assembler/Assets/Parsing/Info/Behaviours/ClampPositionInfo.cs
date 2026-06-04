using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ClampPositionInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Vector3> Min,
		ValueSource<Vector3> Max)
		: BehaviourInfo(Id, Listeners)
	{
		public static ClampPositionInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Min")),
				Transformer.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Max")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new ClampPositionInfo(Id,
				substitutedListeners,
				Min.SubstituteParameters(ctx),
				Max.SubstituteParameters(ctx));
	}
}
