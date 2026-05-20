using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record DeferredTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<float> Delay)
		: BehaviourInfo(Id, Listeners)
	{
		public static DeferredTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<float>(v, props.GetValueOrDefault("Delay"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new DeferredTriggerInfo(Id,
				substitutedListeners,
				Delay.SubstituteParameters(parameters, allValues));
	}
}
