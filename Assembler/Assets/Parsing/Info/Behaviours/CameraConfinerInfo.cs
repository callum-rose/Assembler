using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record CameraConfinerInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		CameraTargetSource Bounds,
		ValueSource<CameraConfinerMode> Mode,
		ValueSource<float> Damping,
		ValueSource<float> Padding) : BehaviourInfo(Id, Listeners)
	{
		public static CameraConfinerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				CameraTargetSource.Parse(ctx, props.GetValueOrDefault("Bounds") ?? NoValue.Instance, id, "Bounds"),
				ValueSourceFactory.CreateEnumSource(ctx, props.GetValueOrDefault("Mode"), CameraConfinerMode.TwoD),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Damping")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Padding")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new CameraConfinerInfo(Id,
				substitutedListeners,
				Bounds.SubstituteParameters(ctx),
				Mode.SubstituteParameters(ctx),
				Damping.SubstituteParameters(ctx),
				Padding.SubstituteParameters(ctx));
	}
}
