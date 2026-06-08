using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ConditionGateInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<bool> Condition)
		: BehaviourInfo(Id, Listeners)
	{
		public static ConditionGateInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<bool>(ctx, props.GetValueOrDefault("Condition")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new ConditionGateInfo(Id,
				substitutedListeners,
				Condition.SubstituteParameters(ctx));
	}
}
