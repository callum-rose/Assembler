using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record TapTriggerInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<float> MaxDuration,
		ValueSource<float> MaxMovement) : BehaviourInfo(Id, Listeners)
	{
		public static TapTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("MaxDuration")),
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("MaxMovement")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new TapTriggerInfo(Id,
				substitutedListeners,
				MaxDuration.SubstituteParameters(ctx),
				MaxMovement.SubstituteParameters(ctx));
	}
}
