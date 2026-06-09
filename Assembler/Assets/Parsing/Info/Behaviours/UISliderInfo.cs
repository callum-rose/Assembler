using System.Collections.Generic;


namespace Assembler.Parsing.Info.Behaviours
{
	public record UISliderInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<float> InitialValue,
		ValueSource<float> MinValue,
		ValueSource<float> MaxValue,
		ValueSource<float> PreferredWidth,
		ValueSource<float> PreferredHeight) : BehaviourInfo(Id, Listeners)
	{
		public static UISliderInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("InitialValue")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("MinValue")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("MaxValue")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("PreferredWidth")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("PreferredHeight")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new UISliderInfo(Id,
				substitutedListeners,
				InitialValue.SubstituteParameters(ctx),
				MinValue.SubstituteParameters(ctx),
				MaxValue.SubstituteParameters(ctx),
				PreferredWidth.SubstituteParameters(ctx),
				PreferredHeight.SubstituteParameters(ctx));
	}
}
