using System.Collections.Generic;


namespace Assembler.Parsing.Info.Behaviours
{
	public record UIContainerInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<string> Direction,
		ValueSource<float> Spacing,
		ValueSource<float> Padding,
		ValueSource<string> ChildAlignment,
		ValueSource<bool> FitContent) : BehaviourInfo(Id, Listeners)
	{
		public static UIContainerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<string>(ctx, props.GetValueOrDefault("Direction")),
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("Spacing")),
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("Padding")),
				Transformer.CreateValueSource<string>(ctx, props.GetValueOrDefault("ChildAlignment")),
				Transformer.CreateValueSource<bool>(ctx, props.GetValueOrDefault("FitContent")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new UIContainerInfo(Id,
				substitutedListeners,
				Direction.SubstituteParameters(ctx),
				Spacing.SubstituteParameters(ctx),
				Padding.SubstituteParameters(ctx),
				ChildAlignment.SubstituteParameters(ctx),
				FitContent.SubstituteParameters(ctx));
	}
}
