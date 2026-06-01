using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Input
{
	/// <summary>Fires every frame the mouse moves, publishing the current position and frame delta.</summary>
	/// <remarks>
	/// Properties:
	/// Outputs:
	///   mouse_position [Vector3]: Current screen-space mouse position.
	///   mouse_delta [Vector3]: Screen-space movement since the previous frame.
	/// </remarks>
	public class MousePositionTrigger : InputTrigger<MousePositionTriggerData>
	{
		private Vector3 _lastPosition;
		private bool _hasLast;

		private void Update()
		{
			if (InputBoundary.ReplayActive)
			{
				return;
			}

			var current = UnityEngine.Input.mousePosition;
			var delta = _hasLast ? current - _lastPosition : Vector3.zero;
			_lastPosition = current;
			_hasLast = true;

			if (delta == Vector3.zero)
			{
				return;
			}

			FireInput(TriggerContext.New(b =>
			{
				b["mouse_position"] = current;
				b["mouse_delta"] = delta;
			}));
		}
	}
}
