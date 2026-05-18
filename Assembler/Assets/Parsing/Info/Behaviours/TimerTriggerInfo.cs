using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record TimerTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<float> Delay)
		: BehaviourInfo(Id, Listeners)
	{
		public static TimerTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<float>(v, props?.GetValueOrDefault("Delay"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new TimerTriggerInfo(Id,
				substitutedListeners,
				Delay.Substitute(parameters, allValues));
	}
}