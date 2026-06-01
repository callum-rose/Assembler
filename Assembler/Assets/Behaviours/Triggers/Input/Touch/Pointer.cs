using UnityEngine;

namespace Assembler.Behaviours.Triggers.Input.Touch
{
	/// <summary>
	/// Stateless reader that unifies the legacy touch and mouse APIs into a single "primary pointer"
	/// (the first finger, or the left mouse button on desktop). Single-finger gestures (tap, swipe,
	/// drag, …) build on this so they remain testable with a mouse in the editor; multi-finger gestures
	/// (pinch) read the touch API directly via <see cref="Count"/> / <see cref="TouchPosition"/>.
	/// Each gesture tracks its own press/elapsed state on top of these per-frame reads.
	/// </summary>
	internal static class Pointer
	{
		/// <summary>True while the primary pointer is down this frame (a live finger, or the left mouse button).</summary>
		public static bool IsPressed =>
			UnityEngine.Input.touchCount > 0
				? UnityEngine.Input.GetTouch(0).phase is not (TouchPhase.Ended or TouchPhase.Canceled)
				: UnityEngine.Input.GetMouseButton(0);

		/// <summary>Screen-space position of the primary pointer this frame.</summary>
		public static Vector2 Position =>
			UnityEngine.Input.touchCount > 0
				? UnityEngine.Input.GetTouch(0).position
				: (Vector2)UnityEngine.Input.mousePosition;

		/// <summary>Number of active touches (always 0 when only a mouse is present).</summary>
		public static int Count => UnityEngine.Input.touchCount;

		/// <summary>Screen-space position of the touch at <paramref name="index"/>.</summary>
		public static Vector2 TouchPosition(int index) => UnityEngine.Input.GetTouch(index).position;
	}
}
