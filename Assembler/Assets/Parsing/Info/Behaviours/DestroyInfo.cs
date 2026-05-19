using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record DestroyInfo(string Id, IReadOnlyList<ListenerInfo> Listeners) : BehaviourInfo(Id, Listeners)
	{
		public static DestroyInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			Dictionary<string, AssemblerValue>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue>? p) =>
			new(id, listeners);

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new DestroyInfo(Id, substitutedListeners);
	}
}