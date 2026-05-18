using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers.Conditionals
{
	public class Condition : Trigger<ConditionData>
	{
		public override void Execute()
		{
			if (Data.Condition.Value)
			{
				InvokeListeners();
			}
		}
	}
}