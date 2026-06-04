using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record WrapPositionInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Vector3> Min,
		ValueSource<Vector3> Max)
		: BehaviourInfo(Id, Listeners)
	{
		public static WrapPositionInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Min")),
				Transformer.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Max")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new WrapPositionInfo(Id,
				substitutedListeners,
				Min.SubstituteParameters(ctx),
				Max.SubstituteParameters(ctx));
	}
}
