using UnityEngine;
using UnityEngine.InputSystem;
using InputSystemPointer = UnityEngine.InputSystem.Pointer;

namespace Assembler.Behaviours.Triggers.Input.Touch
{
	/// <summary>
	/// Stateless reader that unifies mouse and touch into a single "primary pointer" through the Input System's
	/// <see cref="InputSystemPointer"/> device (the last-used pointer: the active finger, or the mouse on
	/// desktop). Single-finger gestures (tap, swipe, drag, …) build on this so they remain testable with a
	/// mouse in the editor; multi-finger gestures (pinch) read <see cref="Touchscreen"/> directly via
	/// <see cref="Count"/> / <see cref="TouchPosition"/>. Each gesture tracks its own press/elapsed state on top
	/// of these per-frame reads.
	/// </summary>
	internal static class Pointer
	{
		/// <summary>True while the primary pointer is down this frame (a live finger, or the left mouse button).</summary>
		public static bool IsPressed => InputSystemPointer.current?.press.isPressed ?? false;

		/// <summary>Screen-space position of the primary pointer this frame (z is 0).</summary>
		public static Vector3 Position =>
			InputSystemPointer.current is { } pointer ? (Vector3)pointer.position.ReadValue() : Vector3.zero;

		/// <summary>Number of touches currently in progress (0 when there is no touchscreen, e.g. mouse-only).</summary>
		public static int Count
		{
			get
			{
				if (Touchscreen.current is not { } touchscreen)
				{
					return 0;
				}

				// Per-frame input read: a manual count avoids the closure a LINQ Count(predicate) allocates.
				var count = 0;
				foreach (var touch in touchscreen.touches)
				{
					if (touch.isInProgress)
					{
						count++;
					}
				}

				return count;
			}
		}

		/// <summary>Screen-space position of the <paramref name="index"/>-th in-progress touch (z is 0).</summary>
		public static Vector3 TouchPosition(int index)
		{
			if (Touchscreen.current is not { } touchscreen)
			{
				return Vector3.zero;
			}

			// touches is a fixed-size pool indexed by slot, not by active order, so walk it counting only the
			// in-progress touches — mapping index to the n-th live finger (matching the old GetTouch(index)).
			var active = 0;
			foreach (var touch in touchscreen.touches)
			{
				if (!touch.isInProgress)
				{
					continue;
				}

				if (active == index)
				{
					return (Vector3)touch.position.ReadValue();
				}

				active++;
			}

			return Vector3.zero;
		}
	}
}
