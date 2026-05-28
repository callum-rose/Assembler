using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Input
{
	/// <summary>Fires on frames where the mouse scroll wheel moved.</summary>
	/// <remarks>
	/// Properties:
	/// Outputs:
	///   scroll_delta [Vector2]: Scroll wheel delta for this frame (y is the common vertical scroll).
	/// </remarks>
	public class ScrollWheelTrigger : InputTrigger<ScrollWheelTriggerData>
	{
		private void Update()
		{
			var delta = UnityEngine.Input.mouseScrollDelta;
			if (delta == Vector2.zero)
			{
				return;
			}

			using (TriggerContext.Push())
			{
				TriggerContext.Set("scroll_delta", delta);
				NotifyListeners();
			}
		}
	}
}
