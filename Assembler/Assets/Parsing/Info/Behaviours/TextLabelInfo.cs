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
				Transformer.CreateOptionalValueSource<string>(ctx, props.GetValueOrDefault("Text")),
				Transformer.CreateOptionalValueSource<int>(ctx, props.GetValueOrDefault("FontSize")),
				Transformer.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("PreferredWidth")),
				Transformer.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("PreferredHeight")));

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
