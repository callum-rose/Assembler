using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Input.Touch
{
	/// <summary>Fires every frame the pointer moves while held down (a drag), reporting the per-frame movement.</summary>
	/// <remarks>
	/// Properties:
	///   Threshold: Screen-space distance, in pixels, the pointer must travel from the press point before drag events start firing. Defaults to 25.
	/// Outputs:
	///   start [Vector2]: Screen-space position where the drag began.
	///   position [Vector2]: Current screen-space pointer position.
	///   delta [Vector2]: Screen-space movement since the previous frame.
	/// </remarks>
	public class Drag : InputTrigger<DragTriggerData>
	{
		private bool _pressed;
		private bool _dragging;
		private Vector2 _startPosition;
		private Vector2 _lastPosition;

		private void Update()
		{
			var pressed = Pointer.IsPressed;
			var position = Pointer.Position;

			if (pressed && !_pressed)
			{
				_startPosition = position;
				_lastPosition = position;
				_dragging = false;
			}
			else if (pressed)
			{
				if (!_dragging)
				{
					var threshold = Data.Threshold.ValueOr(25f);
					if ((position - _startPosition).sqrMagnitude <= threshold * threshold)
					{
						// Still within the dead zone — _pressed is already true, so just wait.
						return;
					}

					// Cleared the threshold — begin dragging, measuring deltas from here so there is no initial jump.
					_dragging = true;
					_lastPosition = position;
				}

				var delta = position - _lastPosition;
				_lastPosition = position;

				if (delta != Vector2.zero)
				{
					NotifyListeners(TriggerContext.New(b =>
					{
						b["start"] = _startPosition;
						b["position"] = position;
						b["delta"] = delta;
					}));
				}
			}

			_pressed = pressed;
		}
	}
}
