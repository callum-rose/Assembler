using System;
using Assembler.Parsing.Phase2.Parsing.Phase2.Info;

namespace AssemblerAlpha.Behaviours.Triggers.Conditionals
{
	public partial class Condition : Trigger<ConditionInfo>
	{
		public Func<bool> _condition;

		protected override void OnInitialise(ConditionInfo behaviourInfo)
		{
		}
		
		public override void Execute()
		{
		}
	}
}