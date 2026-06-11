using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record CameraNoiseInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<string> Profile,
		ValueSource<float> Amplitude,
		ValueSource<float> Frequency) : BehaviourInfo(Id, Listeners)
	{
		public static CameraNoiseInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateOptionalValueSource<string>(ctx, props.GetValueOrDefault("Profile")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Amplitude")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Frequency")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new CameraNoiseInfo(Id,
				substitutedListeners,
				Profile.SubstituteParameters(ctx),
				Amplitude.SubstituteParameters(ctx),
				Frequency.SubstituteParameters(ctx));
	}
}
