using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Input.Touch
{
	/// <summary>Fires every frame two fingers change their separation or orientation (a pinch / zoom and twist).</summary>
	/// <remarks>
	/// Properties:
	/// Outputs:
	///   center [Vector2]: Screen-space midpoint between the two fingers.
	///   distance [float]: Current distance between the two fingers, in pixels.
	///   delta [float]: Change in finger distance since the previous frame (positive = spreading apart).
	///   scale [float]: Ratio of the current distance to the previous frame's (greater than 1 = zooming in).
	///   angle [float]: Current angle of the line between the two fingers, in degrees.
	///   angle_delta [float]: Signed change in that angle since the previous frame, in degrees (positive = counter-clockwise).
	/// </remarks>
	public class Pinch : InputTrigger<PinchTriggerData>
	{
		private bool _tracking;
		private float _lastDistance;
		private float _lastAngle;

		private void Update()
		{
			if (Pointer.Count < 2)
			{
				_tracking = false;
				return;
			}

			var first = Pointer.TouchPosition(0);
			var second = Pointer.TouchPosition(1);
			var line = second - first;
			var distance = line.magnitude;
			var angle = Mathf.Atan2(line.y, line.x) * Mathf.Rad2Deg;

			if (!_tracking)
			{
				_tracking = true;
				_lastDistance = distance;
				_lastAngle = angle;
				return;
			}

			var previous = _lastDistance;
			var distanceDelta = distance - previous;
			var angleDelta = Mathf.DeltaAngle(_lastAngle, angle);
			_lastDistance = distance;
			_lastAngle = angle;

			if (Mathf.Approximately(distanceDelta, 0f) && Mathf.Approximately(angleDelta, 0f))
			{
				return;
			}

			NotifyListeners(TriggerContext.Empty.With(b =>
			{
				b["center"] = (first + second) * 0.5f;
				b["distance"] = distance;
				b["delta"] = distanceDelta;
				b["scale"] = previous > 0f ? distance / previous : 1f;
				b["angle"] = angle;
				b["angle_delta"] = angleDelta;
			}));
		}
	}
}
