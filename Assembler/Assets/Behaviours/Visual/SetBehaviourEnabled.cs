using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Visual
{
	/// <summary>Sets the <c>enabled</c> state of one or more target behaviours to the Enabled value when Executed by an upstream trigger.</summary>
	/// <remarks>
	/// Properties:
	///   Targets: The behaviour(s) to enable/disable — a list of listener-style references (EntityId + BehaviourId, EntityTag, or BehaviourTag). Tag references re-query live state on each Execute, so they pick up matching behaviours added after build. Targets need not be executable, so self-driven behaviours (e.g. velocity) can be toggled.
	///   Enabled: Boolean applied to each target's enabled state on every Execute; true enables, false disables. Disabling stops a behaviour's Unity callbacks (Update etc.), so it halts self-driven behaviours but does not block one from being invoked by a listener.
	/// </remarks>
	public class SetBehaviourEnabled : GameBehaviour<SetBehaviourEnabledData>, IAmExecutable
	{
		public void Execute(TriggerContext ctx)
		{
			var enabled = Data.Enabled.Get(ctx);

			foreach (var target in Data.Targets.Resolve(ctx))
			{
				if (target != null)
				{
					target.enabled = enabled;
				}
			}
		}
	}
}
