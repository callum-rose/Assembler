using System.Collections.Generic;


namespace Assembler.Parsing.Info.Behaviours
{
	public record TextLabelInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<string> Text,
		ValueSource<int> FontSize,
		ValueSource<float> PreferredWidth,
		ValueSource<float> PreferredHeight) : BehaviourInfo(Id, Listeners)
	{
		public static TextLabelInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource(ctx, props.GetValueOrDefault("Text"), fallback: string.Empty),
				Transformer.CreateValueSource(ctx, props.GetValueOrDefault("FontSize"), fallback: 24),
				Transformer.CreateValueSource(ctx, props.GetValueOrDefault("PreferredWidth"), fallback: 0f),
				Transformer.CreateValueSource(ctx, props.GetValueOrDefault("PreferredHeight"), fallback: 0f));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new TextLabelInfo(Id,
				substitutedListeners,
				Text.SubstituteParameters(ctx),
				FontSize.SubstituteParameters(ctx),
				PreferredWidth.SubstituteParameters(ctx),
				PreferredHeight.SubstituteParameters(ctx));
	}
}
