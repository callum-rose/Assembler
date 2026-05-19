using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record RotateTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners) : BehaviourInfo(Id, Listeners)
	{
		public static RotateTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id, listeners);

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new RotateTriggerInfo(Id, substitutedListeners);
	}
}