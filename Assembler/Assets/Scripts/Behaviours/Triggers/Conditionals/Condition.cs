using System;
using Assembler.Generators.Attributes;
using Core;

namespace Behaviours.Triggers.Conditionals
{
	public partial class Condition : Trigger
	{
		[Inject("Condition")] public Func<bool> _condition;
		
		public override void Execute()
		{
		}
	}
}