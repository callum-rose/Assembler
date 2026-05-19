using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record KeyHoldTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<string> Key)
		: BehaviourInfo(Id, Listeners)
	{
		public static KeyHoldTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			Dictionary<string, AssemblerValue>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue>? p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<string>(v, props?.GetValueOrDefault("Key"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new KeyHoldTriggerInfo(Id,
				substitutedListeners,
				Key.SubstituteParameters(parameters, allValues));
	}
}