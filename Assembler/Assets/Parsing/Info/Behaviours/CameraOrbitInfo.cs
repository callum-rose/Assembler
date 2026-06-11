using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record CameraOrbitInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		CameraTargetSource Target,
		ValueSource<float> Radius,
		ValueSource<float> Height,
		ValueSource<float> Damping,
		ValueSource<int> Priority,
		ValueSource<float> Lens) : BehaviourInfo(Id, Listeners)
	{
		public static CameraOrbitInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				CameraTargetSource.Parse(ctx, props.GetValueOrDefault("Target") ?? NoValue.Instance, id, "Target"),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Radius")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Height")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Damping")),
				ValueSourceFactory.CreateOptionalValueSource<int>(ctx, props.GetValueOrDefault("Priority")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Lens")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new CameraOrbitInfo(Id,
				substitutedListeners,
				Target.SubstituteParameters(ctx),
				Radius.SubstituteParameters(ctx),
				Height.SubstituteParameters(ctx),
				Damping.SubstituteParameters(ctx),
				Priority.SubstituteParameters(ctx),
				Lens.SubstituteParameters(ctx));
	}
}
