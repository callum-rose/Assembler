using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record IntervalTriggerInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<float> Interval,
		ValueSource<int> Count,
		ValueSource<bool> AutoStart)
		: BehaviourInfo(Id, Listeners)
	{
		public static IntervalTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<float>(v, props?.GetValueOrDefault("Interval"), parameters: p),
				Transformer.CreateValueSource<int>(v, props?.GetValueOrDefault("Count"), parameters: p),
				Transformer.CreateValueSource<bool>(v, props?.GetValueOrDefault("AutoStart"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new IntervalTriggerInfo(Id,
				substitutedListeners,
				Interval.SubstituteParameters(parameters, allValues),
				Count.SubstituteParameters(parameters, allValues),
				AutoStart.SubstituteParameters(parameters, allValues));
	}
}