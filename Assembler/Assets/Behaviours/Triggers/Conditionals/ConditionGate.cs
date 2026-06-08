using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers.Conditionals
{
	/// <summary>Forwards an upstream trigger to listeners only when Condition evaluates to true at that moment.</summary>
	/// <remarks>
	/// Properties:
	///   Condition: Boolean expression checked on each Execute call.
	/// </remarks>
	public class ConditionGate : Trigger<ConditionGateData>
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
