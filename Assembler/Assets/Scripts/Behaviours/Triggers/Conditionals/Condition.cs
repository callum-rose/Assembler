using System;
using Assembler.Resolving;

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