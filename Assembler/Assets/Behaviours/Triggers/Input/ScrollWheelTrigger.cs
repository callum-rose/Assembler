using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Input
{
	/// <summary>Fires on frames where the mouse scroll wheel moved.</summary>
	/// <remarks>
	/// Properties:
	/// Outputs:
	///   scroll_delta [Vector3]: Scroll wheel delta for this frame (y is the common vertical scroll; z is 0).
	/// </remarks>
	public class ScrollWheelTrigger : InputTrigger<ScrollWheelTriggerData>
	{
		private void Update()
		{
			Vector3 delta = UnityEngine.Input.mouseScrollDelta;
			if (delta == Vector3.zero)
			{
				return;
			}

			NotifyListeners(TriggerContext.New("scroll_delta", delta));
		}
	}
}
