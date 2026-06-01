using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record SwipeTriggerInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<float> MinDistance,
		ValueSource<float> MaxDuration) : BehaviourInfo(Id, Listeners)
	{
		public static SwipeTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("MinDistance")),
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("MaxDuration")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new SwipeTriggerInfo(Id,
				substitutedListeners,
				MinDistance.SubstituteParameters(ctx),
				MaxDuration.SubstituteParameters(ctx));
	}
}
