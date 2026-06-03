using System.Collections.Generic;


namespace Assembler.Parsing.Info.Behaviours
{
	public record UIButtonInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<string> Label,
		ValueSource<float> PreferredWidth,
		ValueSource<float> PreferredHeight) : BehaviourInfo(Id, Listeners)
	{
		public static UIButtonInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<string>(ctx, props.GetValueOrDefault("Label")),
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("PreferredWidth")),
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("PreferredHeight")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new UIButtonInfo(Id,
				substitutedListeners,
				Label.SubstituteParameters(ctx),
				PreferredWidth.SubstituteParameters(ctx),
				PreferredHeight.SubstituteParameters(ctx));
	}
}
