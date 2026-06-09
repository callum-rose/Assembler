using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record CameraInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<CameraProjection> View,
		ValueSource<float> Size,
		ValueSource<float> DefaultBlend) : BehaviourInfo(Id, Listeners)
	{
		public static CameraInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateEnumSource(ctx, props.GetValueOrDefault("View"), CameraProjection.Perspective),
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("Size")),
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("DefaultBlend")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new CameraInfo(Id,
				substitutedListeners,
				View.SubstituteParameters(ctx),
				Size.SubstituteParameters(ctx),
				DefaultBlend.SubstituteParameters(ctx));
	}
}
