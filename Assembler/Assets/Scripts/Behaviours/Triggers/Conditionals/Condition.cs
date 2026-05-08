using System;
using Core;

namespace Behaviours.Triggers.Conditionals
{
	public partial class Condition : Trigger
	{
		public Func<bool> _condition;
		
		public override void Execute()
		{
		}
	}
}