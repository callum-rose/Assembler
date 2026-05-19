using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record TimerTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<float> Delay)
		: BehaviourInfo(Id, Listeners)
	{
		public static TimerTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			Dictionary<string, AssemblerValue>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue>? p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<float>(v, props?.GetValueOrDefault("Delay"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new TimerTriggerInfo(Id,
				substitutedListeners,
				Delay.SubstituteParameters(parameters, allValues));
	}
}