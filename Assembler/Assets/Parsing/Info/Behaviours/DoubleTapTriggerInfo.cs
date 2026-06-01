using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record DoubleTapTriggerInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<float> MaxInterval,
		ValueSource<float> MaxMovement) : BehaviourInfo(Id, Listeners)
	{
		public static DoubleTapTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("MaxInterval")),
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("MaxMovement")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new DoubleTapTriggerInfo(Id,
				substitutedListeners,
				MaxInterval.SubstituteParameters(ctx),
				MaxMovement.SubstituteParameters(ctx));
	}
}
