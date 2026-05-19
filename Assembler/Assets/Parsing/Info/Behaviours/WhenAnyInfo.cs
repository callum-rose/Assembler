using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record WhenAnyInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, IReadOnlyList<string> TriggerIds)
		: BehaviourInfo(Id, Listeners)
	{
		public static WhenAnyInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			Dictionary<string, AssemblerValue>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue>? p) =>
			new(id,
				listeners,
				Transformer.ConvertStringList(props?.GetValueOrDefault("TriggerIds")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new WhenAnyInfo(Id, substitutedListeners, TriggerIds);
	}
}