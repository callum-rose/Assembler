using System;
using System.Collections.Generic;

namespace Assembler.Parsing.Info
{
	public abstract record BehaviourInfo(string Id, IReadOnlyList<ListenerInfo> Listeners)
	{
		public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

		public abstract BehaviourInfo SubstituteParameters(
			IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx);
	}

}
