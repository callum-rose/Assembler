using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record MoveAnimationInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Vector3> Start,
		ValueSource<Vector3> End,
		ValueSource<float> Duration,
		ValueSource<string> Easing) : BehaviourInfo(Id, Listeners)
	{
		public static MoveAnimationInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Start")),
				ValueSourceFactory.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("End")),
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("Duration")),
				ValueSourceFactory.CreateValueSource<string>(ctx, props.GetValueOrDefault("Easing")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new MoveAnimationInfo(Id,
				substitutedListeners,
				Start.SubstituteParameters(ctx),
				End.SubstituteParameters(ctx),
				Duration.SubstituteParameters(ctx),
				Easing.SubstituteParameters(ctx));
	}
}
