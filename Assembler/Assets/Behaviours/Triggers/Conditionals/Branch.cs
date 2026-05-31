using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers.Conditionals
{
	/// <summary>Routes an upstream trigger to its listeners based on a single condition: listeners with <c>When: true</c> (the default) fire when Condition is true, listeners with <c>When: false</c> fire when it is false.</summary>
	/// <remarks>
	/// Replaces the two-gate pattern (a condition gate plus a separately-negated gate) for either/or decisions:
	/// author one Condition and tag each listener with <c>When: true</c> or <c>When: false</c>.
	/// Properties:
	///   Condition: Boolean expression checked on each Execute call to choose the branch.
	/// </remarks>
	public class Branch : Trigger<BranchData>
	{
		public override void Execute(TriggerContext ctx)
		{
			NotifyListeners(ctx, Data.Condition.Get(ctx));
		}
	}
}
