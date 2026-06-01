using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Input.Touch
{
	/// <summary>Fires when the pointer is dragged far enough, fast enough, and then released (a swipe).</summary>
	/// <remarks>
	/// Properties:
	///   MinDistance: Minimum screen-space travel, in pixels, required to count as a swipe. Defaults to 75.
	///   MaxDuration: Longest press, in seconds, that still counts as a swipe rather than a slow drag. Defaults to 0.5.
	/// Outputs:
	///   start [Vector2]: Screen-space position where the swipe began.
	///   position [Vector2]: Screen-space position where the swipe ended.
	///   delta [Vector2]: Vector from start to end.
	///   distance [float]: Length of the swipe in pixels.
	///   direction [Vector2]: Normalised swipe direction.
	/// </remarks>
	public class Swipe : InputTrigger<SwipeTriggerData>, INeedsGameClock
	{
		public IGameClock Clock { get; set; } = null!;

		private bool _pressed;
		private double _startTime;
		private Vector2 _startPosition;

		private void Update()
		{
			var pressed = Pointer.IsPressed;
			var position = Pointer.Position;

			if (pressed && !_pressed)
			{
				_startTime = Clock.Time;
				_startPosition = position;
			}
			else if (!pressed && _pressed)
			{
				var delta = position - _startPosition;
				var distance = delta.magnitude;
				var withinTime = Clock.Time - _startTime <= Data.MaxDuration.ValueOr(0.5f);

				if (distance >= Data.MinDistance.ValueOr(75f) && withinTime)
				{
					NotifyListeners(TriggerContext.New(b =>
					{
						b["start"] = _startPosition;
						b["position"] = position;
						b["delta"] = delta;
						b["distance"] = distance;
						b["direction"] = delta.normalized;
					}));
				}
			}

			_pressed = pressed;
		}
	}
}
