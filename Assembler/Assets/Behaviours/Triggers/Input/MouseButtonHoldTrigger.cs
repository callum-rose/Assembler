using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Input
{
	/// <summary>Fires every frame while the given mouse button is held down.</summary>
	/// <remarks>
	/// Properties:
	///   Button: Mouse button index — 0 (left), 1 (right), 2 (middle).
	/// Outputs:
	///   mouse_position [Vector3]: Current screen-space mouse position.
	/// </remarks>
	public class MouseButtonHoldTrigger : InputTrigger<MouseButtonHoldTriggerData>
	{
		private void Update()
		{
			if (UnityEngine.Input.GetMouseButton(Data.Button.Value))
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
