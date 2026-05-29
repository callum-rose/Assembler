using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Input
{
	/// <summary>Fires on a gamepad / joystick button event (press, release, or hold).</summary>
	/// <remarks>
	/// Properties:
	///   Button: Unity key string for the gamepad button (e.g. "joystick button 0", "joystick 1 button 1").
	///   Mode: When to fire — "down" (press only), "up" (release only), or "hold" (every frame held). Defaults to "down".
	/// </remarks>
	public class GamepadButtonTrigger : InputTrigger<GamepadButtonTriggerData>
	{
		private void Update()
		{
			var button = Data.Button.Get();
			if (string.IsNullOrEmpty(button))
			{
				return;
			}

			var mode = Data.Mode.ValueOr("down");
			var fired = mode switch
			{
				"up" => UnityEngine.Input.GetKeyUp(button),
				"hold" => UnityEngine.Input.GetKey(button),
				_ => UnityEngine.Input.GetKeyDown(button)
			};

			if (fired)
			{
				NotifyListeners(TriggerContext.Empty);
			}
		}
	}
}
