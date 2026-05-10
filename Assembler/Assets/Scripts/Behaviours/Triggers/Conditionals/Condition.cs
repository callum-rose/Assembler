using System;
using Assembler.Parsing.Phase2.Info;
using Assembler.Parsing.Phase3;

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