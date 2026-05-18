using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record KeyDownTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, ValueSource<string> Key)
		: BehaviourInfo(Id, Listeners)
	{
		public static KeyDownTriggerInfo Create(string id,
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
			new KeyDownTriggerInfo(Id,
				substitutedListeners,
				Key.Substitute(parameters, allValues));
	}
}