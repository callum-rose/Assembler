using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ProgressBarInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<float> Value,
		ScreenRect Rect) : BehaviourInfo(Id, Listeners)
	{
		public static ProgressBarInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("Value")),
				ScreenRectParser.Parse(props.GetValueOrDefault("Rect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new ProgressBarInfo(Id,
				substitutedListeners,
				Value.SubstituteParameters(ctx),
				Rect);
	}
}
