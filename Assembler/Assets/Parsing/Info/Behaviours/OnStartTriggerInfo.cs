using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record OnStartTriggerInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners) : BehaviourInfo(Id, Listeners)
	{
		public static OnStartTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id, listeners);

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new OnStartTriggerInfo(Id, substitutedListeners);
	}
}