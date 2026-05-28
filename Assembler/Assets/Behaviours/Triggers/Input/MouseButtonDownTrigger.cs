using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Input
{
	/// <summary>Fires on the frame the given mouse button is pressed down.</summary>
	/// <remarks>
	/// Properties:
	///   Button: Mouse button index — 0 (left), 1 (right), 2 (middle).
	/// Outputs:
	///   mouse_position [Vector3]: Screen-space mouse position at the moment of the click.
	/// </remarks>
	public class MouseButtonDownTrigger : InputTrigger<MouseButtonDownTriggerData>
	{
		private void Update()
		{
			if (UnityEngine.Input.GetMouseButtonDown(Data.Button.Value))
			{
				TriggerContext.Push();
				try
				{
					TriggerContext.Set("mouse_position", UnityEngine.Input.mousePosition);
					NotifyListeners();
				}
				finally
				{
					TriggerContext.Pop();
				}
			}
		}
	}
}
