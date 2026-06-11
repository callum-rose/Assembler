using System;
using System.Collections.Generic;

namespace Assembler.Parsing.Info
{
	public abstract record BehaviourInfo(string Id, IReadOnlyList<ListenerInfo> Listeners)
	{
		public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

		/// <summary>
		/// Listeners owned by this behaviour beyond the top-level <see cref="Listeners"/> set — e.g. a state
		/// machine's per-state OnEnter/OnExit hooks. Empty for most behaviours; overridden where a behaviour
		/// nests listener sites of its own, so whole-descriptor scans (such as the build-time game-over guard)
		/// see every listener rather than only the top-level ones.
		/// </summary>
		public virtual IEnumerable<ListenerInfo> NestedListeners => Array.Empty<ListenerInfo>();

		public abstract BehaviourInfo SubstituteParameters(
			IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx);
	}

}
