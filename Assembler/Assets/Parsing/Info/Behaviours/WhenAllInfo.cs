using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record WhenAllInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, IReadOnlyList<string> TriggerIds)
		: BehaviourInfo(Id, Listeners)
	{
		public static WhenAllInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.ConvertStringList(props.GetValueOrDefault("TriggerIds")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new WhenAllInfo(Id, substitutedListeners, TriggerIds);
	}
}
