using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record CameraFollowInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		CameraTargetSource Target,
		CameraTargetSource LookAt,
		ValueSource<string> Mode,
		ValueSource<int> Priority,
		ValueSource<float> Lens,
		ValueSource<float> Damping,
		ValueSource<float> DeadZone,
		ValueSource<float> CameraDistance,
		ValueSource<Vector3> ScreenOffset,
		ValueSource<Vector3> FollowOffset) : BehaviourInfo(Id, Listeners)
	{
		public static CameraFollowInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				CameraTargetSource.Parse(ctx, props.GetValueOrDefault("Target") ?? NoValue.Instance, id, "Target"),
				CameraTargetSource.Parse(ctx, props.GetValueOrDefault("LookAt") ?? NoValue.Instance, id, "LookAt"),
				Transformer.CreateValueSource<string>(ctx, props.GetValueOrDefault("Mode")),
				Transformer.CreateValueSource<int>(ctx, props.GetValueOrDefault("Priority")),
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("Lens")),
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("Damping")),
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("DeadZone")),
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("CameraDistance")),
				Transformer.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("ScreenOffset")),
				Transformer.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("FollowOffset")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new CameraFollowInfo(Id,
				substitutedListeners,
				Target.SubstituteParameters(ctx),
				LookAt.SubstituteParameters(ctx),
				Mode.SubstituteParameters(ctx),
				Priority.SubstituteParameters(ctx),
				Lens.SubstituteParameters(ctx),
				Damping.SubstituteParameters(ctx),
				DeadZone.SubstituteParameters(ctx),
				CameraDistance.SubstituteParameters(ctx),
				ScreenOffset.SubstituteParameters(ctx),
				FollowOffset.SubstituteParameters(ctx));
	}
}
