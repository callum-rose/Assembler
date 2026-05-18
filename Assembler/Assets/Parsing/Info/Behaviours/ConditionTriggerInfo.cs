using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ConditionTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<bool> Condition)
		: BehaviourInfo(Id, Listeners)
	{
		public static ConditionTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<bool>(v, props?.GetValueOrDefault("Condition"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new ConditionTriggerInfo(Id,
				substitutedListeners,
				Condition.Substitute(parameters, allValues));
	}
}