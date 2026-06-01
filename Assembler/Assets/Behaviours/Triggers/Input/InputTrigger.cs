
using System;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers.Input
{
	/// <summary>
	/// Base class for triggers that fire from player input (keyboard, mouse, gamepad, touch gestures). These cannot
	/// be executed manually (<see cref="Execute"/> throws); subclasses notify listeners when their input is detected.
	/// </summary>
	public abstract class InputTrigger<T> : Trigger<T> where T : TriggerData
	{
		public override void Execute(TriggerContext ctx)
		{
			throw new Exception("Cannot execute an input trigger manually");
		}
	}
}
