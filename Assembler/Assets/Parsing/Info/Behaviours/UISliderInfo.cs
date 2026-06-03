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
				Transformer.CreateValueSource(ctx, props.GetValueOrDefault("InitialValue"), fallback: 0f),
				Transformer.CreateValueSource(ctx, props.GetValueOrDefault("MinValue"), fallback: 0f),
				Transformer.CreateValueSource(ctx, props.GetValueOrDefault("MaxValue"), fallback: 1f),
				Transformer.CreateValueSource(ctx, props.GetValueOrDefault("PreferredWidth"), fallback: 0f),
				Transformer.CreateValueSource(ctx, props.GetValueOrDefault("PreferredHeight"), fallback: 0f));

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
