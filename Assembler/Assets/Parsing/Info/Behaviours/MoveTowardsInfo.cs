using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record MoveTowardsInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Vector3> Target,
		ValueSource<float> Speed)
		: BehaviourInfo(Id, Listeners)
	{
		public static MoveTowardsInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Target")),
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("Speed")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new MoveTowardsInfo(Id,
				substitutedListeners,
				Target.SubstituteParameters(ctx),
				Speed.SubstituteParameters(ctx));
	}
}
