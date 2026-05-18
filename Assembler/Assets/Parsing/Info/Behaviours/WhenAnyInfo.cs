using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record WhenAnyInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners, IReadOnlyList<string> TriggerIds)
		: BehaviourInfo(Id, Listeners)
	{
		public static WhenAnyInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.ConvertStringList(props?.GetValueOrDefault("TriggerIds")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new WhenAnyInfo(Id, substitutedListeners, TriggerIds);
	}
}