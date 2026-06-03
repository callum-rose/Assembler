using System.Collections.Generic;


namespace Assembler.Parsing.Info.Behaviours
{
	public record UICanvasInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<float> MatchWidthOrHeight) : BehaviourInfo(Id, Listeners)
	{
		public static UICanvasInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource(ctx, props.GetValueOrDefault("MatchWidthOrHeight"), fallback: 0.5f));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new UICanvasInfo(Id,
				substitutedListeners,
				MatchWidthOrHeight.SubstituteParameters(ctx));
	}
}
