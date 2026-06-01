using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record DragTriggerInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<float> Threshold) : BehaviourInfo(Id, Listeners)
	{
		public static DragTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("Threshold")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new DragTriggerInfo(Id,
				substitutedListeners,
				Threshold.SubstituteParameters(ctx));
	}
}
