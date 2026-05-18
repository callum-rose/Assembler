using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record TriggerStayTriggerInfo(
		string Id,
		IReadOnlyList<BehaviourDescriptor> Listeners,
		IReadOnlyList<string> TagsToDetect) : BehaviourInfo(Id, Listeners)
	{
		public static TriggerStayTriggerInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.ConvertStringList(props?.GetValueOrDefault("TagsToDetect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new TriggerStayTriggerInfo(Id, substitutedListeners, TagsToDetect);
	}
}