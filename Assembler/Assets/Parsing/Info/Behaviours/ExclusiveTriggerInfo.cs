using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ExclusiveTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<string> Group)
		: BehaviourInfo(Id, Listeners)
	{
		public static ExclusiveTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<string>(v, props.GetValueOrDefault("Group"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new ExclusiveTriggerInfo(Id,
				substitutedListeners,
				Group.SubstituteParameters(parameters, allValues));
	}
}
