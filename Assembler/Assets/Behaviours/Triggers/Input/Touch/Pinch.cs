using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Input.Touch
{
	/// <summary>Fires every frame two fingers move closer together or further apart (a pinch / zoom).</summary>
	/// <remarks>
	/// Properties:
	/// Outputs:
	///   center [Vector2]: Screen-space midpoint between the two fingers.
	///   distance [float]: Current distance between the two fingers, in pixels.
	///   delta [float]: Change in finger distance since the previous frame (positive = spreading apart).
	///   scale [float]: Ratio of the current distance to the previous frame's (greater than 1 = zooming in).
	/// </remarks>
	public class Pinch : InputTrigger<PinchTriggerData>
	{
		private bool _tracking;
		private float _lastDistance;

		private void Update()
		{
			if (Pointer.Count < 2)
			{
				_tracking = false;
				return;
			}

			var first = Pointer.TouchPosition(0);
			var second = Pointer.TouchPosition(1);
			var distance = Vector2.Distance(first, second);

			if (!_tracking)
			{
				_tracking = true;
				_lastDistance = distance;
				return;
			}

			var previous = _lastDistance;
			var delta = distance - previous;
			_lastDistance = distance;

			if (Mathf.Approximately(delta, 0f))
			{
				return;
			}

			NotifyListeners(TriggerContext.Empty.With(b =>
			{
				b["center"] = (first + second) * 0.5f;
				b["distance"] = distance;
				b["delta"] = delta;
				b["scale"] = previous > 0f ? distance / previous : 1f;
			}));
		}
	}
}
