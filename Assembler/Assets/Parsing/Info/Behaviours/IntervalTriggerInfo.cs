using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record IntervalTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<float> Interval)
		: BehaviourInfo(Id, Listeners)
	{
		public static IntervalTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<float>(v, props?.GetValueOrDefault("Interval"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new IntervalTriggerInfo(Id,
				substitutedListeners,
				Interval.Substitute(parameters, allValues));
	}
}