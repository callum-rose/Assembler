using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Input.Touch
{
	/// <summary>Fires once when the pointer is pressed and released quickly without moving (a tap).</summary>
	/// <remarks>
	/// Properties:
	///   MaxDuration: Longest press, in seconds, that still counts as a tap. Defaults to 0.3.
	///   MaxMovement: Largest screen-space drift, in pixels, allowed during the press. Defaults to 25.
	/// Outputs:
	///   position [Vector3]: Screen-space position where the tap was released (z is 0).
	/// </remarks>
	public class Tap : InputTrigger<TapTriggerData>, INeedsGameClock
	{
		public IGameClock Clock { get; set; } = null!;

		private bool _pressed;
		private double _startTime;
		private Vector3 _startPosition;
		private float _maxMoveSqr;

		private void Update()
		{
			var pressed = Pointer.IsPressed;
			var position = Pointer.Position;

			if (pressed && !_pressed)
			{
				_startTime = Clock.Time;
				_startPosition = position;
				_maxMoveSqr = 0f;
			}
			else if (pressed)
			{
				_maxMoveSqr = Mathf.Max(_maxMoveSqr, (position - _startPosition).sqrMagnitude);
			}
			else if (_pressed)
			{
				var maxMovement = Data.MaxMovement.ValueOr(25f);
				var withinTime = Clock.Time - _startTime <= Data.MaxDuration.ValueOr(0.3f);
				var withinMovement = _maxMoveSqr <= maxMovement * maxMovement;

				if (withinTime && withinMovement)
				{
					NotifyListeners(TriggerContext.New("position", position));
				}
			}

			_pressed = pressed;
		}
	}
}
