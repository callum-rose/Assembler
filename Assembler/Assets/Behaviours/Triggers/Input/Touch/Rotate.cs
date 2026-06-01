using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Input.Touch
{
	/// <summary>Fires every frame two fingers twist around each other (a rotate gesture).</summary>
	/// <remarks>
	/// Properties:
	/// Outputs:
	///   center [Vector2]: Screen-space midpoint between the two fingers.
	///   angle_delta [float]: Signed rotation since the previous frame, in degrees (positive = counter-clockwise).
	/// </remarks>
	public class Rotate : InputTrigger<RotateTriggerData>
	{
		private bool _tracking;
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
			var direction = second - first;
			var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

			if (!_tracking)
			{
				_tracking = true;
				_lastAngle = angle;
				return;
			}

			var delta = Mathf.DeltaAngle(_lastAngle, angle);
			_lastAngle = angle;

			if (Mathf.Approximately(delta, 0f))
			{
				return;
			}

			NotifyListeners(TriggerContext.Empty.With(b =>
			{
				b["center"] = (first + second) * 0.5f;
				b["angle_delta"] = delta;
			}));
		}
	}
}
