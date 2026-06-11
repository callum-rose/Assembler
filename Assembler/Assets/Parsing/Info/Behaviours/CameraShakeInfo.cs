using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record CameraShakeInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<float> Force,
		ValueSource<float> Duration,
		ValueSource<Vector3> Velocity) : BehaviourInfo(Id, Listeners)
	{
		public static CameraShakeInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Force")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Duration")),
				ValueSourceFactory.CreateOptionalValueSource<Vector3>(ctx, props.GetValueOrDefault("Velocity")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new CameraShakeInfo(Id,
				substitutedListeners,
				Force.SubstituteParameters(ctx),
				Duration.SubstituteParameters(ctx),
				Velocity.SubstituteParameters(ctx));
	}
}
