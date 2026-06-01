using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Input.Touch
{
	/// <summary>Fires when two quick taps land close together within a short interval (a double-tap).</summary>
	/// <remarks>
	/// Properties:
	///   MaxInterval: Longest gap, in seconds, allowed between the two taps. Defaults to 0.3.
	///   MaxMovement: Largest screen-space drift, in pixels, allowed both during a tap and between the two taps. Defaults to 25.
	/// Outputs:
	///   position [Vector2]: Screen-space position where the second tap was released.
	/// </remarks>
	public class DoubleTap : InputTrigger<DoubleTapTriggerData>, INeedsGameClock
	{
		// A single tap's press may last at most this long; the configurable MaxInterval governs the gap between taps.
		private const float TapMaxDuration = 0.3f;

		public IGameClock Clock { get; set; } = null!;

		private bool _pressed;
		private double _pressStartTime;
		private Vector2 _pressStartPosition;
		private float _pressMoveSqr;

		private bool _hasFirstTap;
		private double _firstTapTime;
		private Vector2 _firstTapPosition;

		private void Update()
		{
			var pressed = Pointer.IsPressed;
			var position = Pointer.Position;
			var maxMovement = Data.MaxMovement.ValueOr(25f);
			var maxMoveSqr = maxMovement * maxMovement;

			if (pressed && !_pressed)
			{
				_pressStartTime = Clock.Time;
				_pressStartPosition = position;
				_pressMoveSqr = 0f;
			}
			else if (pressed)
			{
				_pressMoveSqr = Mathf.Max(_pressMoveSqr, (position - _pressStartPosition).sqrMagnitude);
			}
			else if (_pressed)
			{
				var isTap = Clock.Time - _pressStartTime <= TapMaxDuration && _pressMoveSqr <= maxMoveSqr;

				if (!isTap)
				{
					_hasFirstTap = false;
				}
				else if (_hasFirstTap &&
				         Clock.Time - _firstTapTime <= Data.MaxInterval.ValueOr(0.3f) &&
				         (position - _firstTapPosition).sqrMagnitude <= maxMoveSqr)
				{
					NotifyListeners(TriggerContext.Empty.With("position", position));
					_hasFirstTap = false;
				}
				else
				{
					_hasFirstTap = true;
					_firstTapTime = Clock.Time;
					_firstTapPosition = position;
				}
			}

			_pressed = pressed;
		}
	}
}
