using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record CameraGroupInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<string> Tag,
		ValueSource<int> Priority,
		ValueSource<float> Damping,
		ValueSource<float> FramingSize,
		ValueSource<float> Lens) : BehaviourInfo(Id, Listeners)
	{
		public static CameraGroupInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<string>(ctx, props.GetValueOrDefault("Tag") ?? NoValue.Instance),
				ValueSourceFactory.CreateOptionalValueSource<int>(ctx, props.GetValueOrDefault("Priority")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Damping")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("FramingSize")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Lens")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new CameraGroupInfo(Id,
				substitutedListeners,
				Tag.SubstituteParameters(ctx),
				Priority.SubstituteParameters(ctx),
				Damping.SubstituteParameters(ctx),
				FramingSize.SubstituteParameters(ctx),
				Lens.SubstituteParameters(ctx));
	}
}
