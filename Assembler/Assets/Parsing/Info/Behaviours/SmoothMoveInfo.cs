using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record SmoothMoveInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Vector3> Target,
		ValueSource<float> SmoothTime)
		: BehaviourInfo(Id, Listeners)
	{
		public static SmoothMoveInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Target")),
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("SmoothTime")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new SmoothMoveInfo(Id,
				substitutedListeners,
				Target.SubstituteParameters(ctx),
				SmoothTime.SubstituteParameters(ctx));
	}
}
