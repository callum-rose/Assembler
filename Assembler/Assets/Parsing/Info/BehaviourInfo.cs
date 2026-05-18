using System.Collections.Generic;

namespace Assembler.Parsing.Info
{
	public abstract record BehaviourInfo(string Id, IReadOnlyList<BehaviourDescriptor> Listeners)
	{
		public abstract BehaviourInfo SubstituteParameters(
			IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues);
	}

}