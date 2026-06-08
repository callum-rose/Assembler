using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record TriggerExitTriggerInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		IReadOnlyList<string> TagsToDetect) : BehaviourInfo(Id, Listeners)
	{
		public static TriggerExitTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.ConvertStringList(props.GetValueOrDefault("TagsToDetect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new TriggerExitTriggerInfo(Id, substitutedListeners, TagsToDetect);
	}
}
