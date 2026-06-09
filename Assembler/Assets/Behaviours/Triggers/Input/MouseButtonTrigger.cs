using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Input
{
	/// <summary>Fires on a mouse button event during the selected phase (press, release, or hold).</summary>
	/// <remarks>
	/// Properties:
	///   Button: Mouse button index — 0 (left), 1 (right), 2 (middle).
	///   Phase: When to fire — "down" (press only), "up" (release only), or "hold" (every frame held). Defaults to "down".
	/// Outputs:
	///   mouse_position [Vector3]: Screen-space mouse position when the trigger fires.
	/// </remarks>
	public class MouseButtonTrigger : InputTrigger<MouseButtonTriggerData>
	{
		private void Update()
		{
			var button = Data.Button.Get();
			var fired = Data.Phase.Get() switch
			{
				ButtonPhase.Up => UnityEngine.Input.GetMouseButtonUp(button),
				ButtonPhase.Hold => UnityEngine.Input.GetMouseButton(button),
				_ => UnityEngine.Input.GetMouseButtonDown(button)
			};

			if (!fired)
			{
				return;
			}

			NotifyListeners(TriggerContext.New("mouse_position", UnityEngine.Input.mousePosition));
		}
	}
}
