
using System;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers.Input
{
	public abstract class InputTrigger<T> : Trigger<T> where T : TriggerData
	{
		public override void Execute()
		{
			throw new Exception("Cannot execute an input trigger manually");
		}
	}
}
