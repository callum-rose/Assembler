using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Input.Touch
{
	/// <summary>Fires once when the pointer is held still for a threshold time (a long press).</summary>
	/// <remarks>
	/// Properties:
	///   Duration: Seconds the pointer must be held before the trigger fires. Defaults to 0.5.
	///   MaxMovement: Largest screen-space drift, in pixels, allowed while holding; moving further cancels the press. Defaults to 25.
	/// Outputs:
	///   position [Vector2]: Screen-space position of the press when the threshold was reached.
	///   hold_duration [float]: Seconds the pointer had been held when the trigger fired (at least Duration).
	/// </remarks>
	public class LongPress : InputTrigger<LongPressTriggerData>, INeedsGameClock
	{
		public IGameClock Clock { get; set; } = null!;

		private bool _pressed;
		private double _startTime;
		private Vector2 _startPosition;
		private bool _resolved;

		private void Update()
		{
			if (InputBoundary.ReplayActive)
			{
				return;
			}

			var pressed = Pointer.IsPressed;
			var position = Pointer.Position;

			if (pressed && !_pressed)
			{
				_startTime = Clock.Time;
				_startPosition = position;
				_resolved = false;
			}
			else if (pressed && !_resolved)
			{
				var maxMovement = Data.MaxMovement.ValueOr(25f);

				if ((position - _startPosition).sqrMagnitude > maxMovement * maxMovement)
				{
					// Drifted too far to be a long press — give up until the pointer is released.
					_resolved = true;
				}
				else
				{
					var held = Clock.Time - _startTime;
					if (held >= Data.Duration.ValueOr(0.5f))
					{
						FireInput(TriggerContext.New(b =>
						{
							b["position"] = position;
							b["hold_duration"] = (float)held;
						}));
						_resolved = true;
					}
				}
			}

			_pressed = pressed;
		}
	}
}
