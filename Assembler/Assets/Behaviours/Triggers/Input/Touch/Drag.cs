using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Input.Touch
{
	/// <summary>Fires every frame the pointer moves while held down (a drag), reporting the per-frame movement.</summary>
	/// <remarks>
	/// Properties:
	/// Outputs:
	///   start [Vector2]: Screen-space position where the drag began.
	///   position [Vector2]: Current screen-space pointer position.
	///   delta [Vector2]: Screen-space movement since the previous frame.
	/// </remarks>
	public class Drag : InputTrigger<DragTriggerData>
	{
		private bool _pressed;
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
			}
			else if (pressed)
			{
				var delta = position - _lastPosition;
				_lastPosition = position;

				if (delta != Vector2.zero)
				{
					NotifyListeners(TriggerContext.Empty.With(b =>
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
