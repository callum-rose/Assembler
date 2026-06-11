using System;
using System.Collections.Generic;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Visual
{
	/// <summary>Flips the <c>enabled</c> state of one or more target behaviours each time it is Executed by an upstream trigger.</summary>
	/// <remarks>
	/// Properties:
	///   Targets: The behaviour(s) to toggle — a list of listener-style references (EntityId + BehaviourId, EntityTag, or BehaviourTag). Tag references re-query live state on each Execute, so they pick up matching behaviours added after build. Targets need not be executable, so self-driven behaviours (e.g. velocity) can be toggled. Each target is flipped relative to its own current state.
	/// </remarks>
	public class ToggleBehaviourEnabled : GameBehaviour<ToggleBehaviourEnabledData>, IAmExecutable
	{
		// The behaviours to toggle, resolved from the Targets: references. Wired by the build factory
		// (it reuses the listener machinery but does not require the targets to be executable).
		public IReadOnlyList<Listener> Targets { get; set; } = Array.Empty<Listener>();

		public void Execute(TriggerContext ctx)
		{
			foreach (var listener in Targets)
			{
				foreach (var target in listener.ResolveTargets(ctx))
				{
					if (target != null)
					{
						target.enabled = !target.enabled;
					}
				}
			}
		}
	}
}
