
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers.Input
{
	/// <summary>
	/// Base class for triggers that fire from player input (keyboard, mouse, gamepad, touch gestures). These are
	/// event sources: subclasses notify listeners when their input is detected. They expose no Execute and are not
	/// valid Listeners: targets.
	/// </summary>
	public abstract class InputTrigger<T> : Trigger<T> where T : TriggerData
	{
	}
}
