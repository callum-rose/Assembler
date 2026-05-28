using System.Collections.Generic;


namespace Assembler.Parsing.Info.Behaviours
{
	public record UISliderInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<float> InitialValue,
		ValueSource<float> MinValue,
		ValueSource<float> MaxValue,
		ScreenRect Rect) : BehaviourInfo(Id, Listeners)
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
				ScreenRectParser.Parse(props.GetValueOrDefault("Rect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new UISliderInfo(Id,
				substitutedListeners,
				InitialValue.SubstituteParameters(ctx),
				MinValue.SubstituteParameters(ctx),
				MaxValue.SubstituteParameters(ctx),
				Rect);
	}
}
