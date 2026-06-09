using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record RotateAnimationInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Vector3> Start,
		ValueSource<Vector3> End,
		ValueSource<float> Duration,
		ValueSource<Easing> Easing) : BehaviourInfo(Id, Listeners)
	{
		public static RotateAnimationInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Start")),
				ValueSourceFactory.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("End")),
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("Duration")),
				ValueSourceFactory.CreateEnumSource(ctx, props.GetValueOrDefault("Easing"), Behaviours.Easing.InOutSine));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new RotateAnimationInfo(Id,
				substitutedListeners,
				Start.SubstituteParameters(ctx),
				End.SubstituteParameters(ctx),
				Duration.SubstituteParameters(ctx),
				Easing.SubstituteParameters(ctx));
	}
}
