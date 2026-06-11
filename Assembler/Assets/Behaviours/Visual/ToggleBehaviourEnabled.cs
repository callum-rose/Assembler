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
		public void Execute(TriggerContext ctx)
		{
			foreach (var target in Data.Targets.Resolve(ctx))
			{
				if (target != null)
				{
					target.enabled = !target.enabled;
				}
			}
		}
	}
}
