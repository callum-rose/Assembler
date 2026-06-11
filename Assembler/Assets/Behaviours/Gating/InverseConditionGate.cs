using Assembler.Behaviours.Triggers;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Gating
{
	/// <summary>Forwards an upstream trigger to listeners only when Condition evaluates to false at that moment.</summary>
	/// <remarks>
	/// The mirror of `condition gate`. Pair the two, both referencing the same boolean expression,
	/// to express an either/or decision without authoring a separate negated expression.
	/// Properties:
	///   Condition: Boolean expression checked on each Execute call; listeners fire when it is false.
	/// </remarks>
	public class InverseConditionGate : Trigger<ConditionGateData>, IAmExecutable
	{
		public void Execute(TriggerContext ctx)
		{
			if (!Data.Condition.Get(ctx))
			{
				NotifyListeners(ctx);
			}
		}
	}
}
