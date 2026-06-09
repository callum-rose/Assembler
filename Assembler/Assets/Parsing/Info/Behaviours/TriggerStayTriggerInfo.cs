using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record TriggerStayTriggerInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		IReadOnlyList<string> TagsToDetect) : BehaviourInfo(Id, Listeners)
	{
		public static TriggerStayTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.ConvertStringList(props.GetValueOrDefault("TagsToDetect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new TriggerStayTriggerInfo(Id, substitutedListeners, TagsToDetect);
	}
}
