using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record KeyHoldTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<string> Key)
		: BehaviourInfo(Id, Listeners)
	{
		public static KeyHoldTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<string>(v, props?.GetValueOrDefault("Key"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new KeyHoldTriggerInfo(Id,
				substitutedListeners,
				Key.Substitute(parameters, allValues));
	}
}