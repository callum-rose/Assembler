using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record LongPressTriggerInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<float> Duration,
		ValueSource<float> MaxMovement) : BehaviourInfo(Id, Listeners)
	{
		public static LongPressTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("Duration")),
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("MaxMovement")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new LongPressTriggerInfo(Id,
				substitutedListeners,
				Duration.SubstituteParameters(ctx),
				MaxMovement.SubstituteParameters(ctx));
	}
}
