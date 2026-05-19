using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ConditionTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<bool> Condition)
		: BehaviourInfo(Id, Listeners)
	{
		public static ConditionTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<bool>(v, props.GetValueOrDefault("Condition"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new ConditionTriggerInfo(Id,
				substitutedListeners,
				Condition.SubstituteParameters(parameters, allValues));
	}
}