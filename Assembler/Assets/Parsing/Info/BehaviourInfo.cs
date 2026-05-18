using System.Collections.Generic;

namespace Assembler.Parsing.Info
{
	public abstract record BehaviourInfo(string Id, IReadOnlyList<ListenerInfo> Listeners)
	{
		public abstract BehaviourInfo SubstituteParameters(
			IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues);
	}

}