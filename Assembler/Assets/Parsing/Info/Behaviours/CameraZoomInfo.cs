using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record CameraZoomInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<float> Width,
		ValueSource<float> Damping,
		ValueSource<float> MinFOV,
		ValueSource<float> MaxFOV) : BehaviourInfo(Id, Listeners)
	{
		public static CameraZoomInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Width")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Damping")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("MinFOV")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("MaxFOV")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new CameraZoomInfo(Id,
				substitutedListeners,
				Width.SubstituteParameters(ctx),
				Damping.SubstituteParameters(ctx),
				MinFOV.SubstituteParameters(ctx),
				MaxFOV.SubstituteParameters(ctx));
	}
}
