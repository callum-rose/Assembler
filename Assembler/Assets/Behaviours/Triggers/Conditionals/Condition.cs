using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers.Conditionals
{
	/// <summary>
	/// Forwards an upstream trigger to its listeners only when the named boolean expression evaluates to
	/// true at that moment. Like <c>condition gate</c>, but the predicate is a declared expression invoked
	/// by id with explicit arguments rather than an inline <c>!expr</c>.
	/// </summary>
	/// <remarks>
	/// Properties:
	///   ExpressionId: Id (or CallableAs alias) of a declared expression returning bool.
	///   Arguments [list]: Operands passed positionally to that expression each time the gate is evaluated.
	/// </remarks>
	public class Condition : Trigger<ConditionGateData>
	{
		public override void Execute(TriggerContext ctx)
		{
			if (Data.Condition.Get(ctx))
			{
				NotifyListeners(ctx);
			}
		}
	}
}
