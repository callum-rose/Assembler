using UnityEngine;

namespace Assembler.Behaviours.UI.Internal
{
	/// <summary>
	/// Drives its <see cref="RectTransform"/> to track the device's <see cref="Screen.safeArea"/> so anything
	/// parented under it stays clear of notches, punch-holes and rounded corners. On a screen-space-overlay
	/// canvas the canvas rect maps 1:1 to the screen's pixels, so the safe-area pixel rect normalises directly
	/// into anchor coordinates. Off mobile (and in the editor) <c>safeArea</c> equals the full screen, so this
	/// is a no-op there. Re-applies only when the safe area or screen dimensions actually change (orientation
	/// flips, resolution changes), so the per-frame cost is a couple of comparisons.
	/// </summary>
	[RequireComponent(typeof(RectTransform))]
	public sealed class SafeAreaFitter : MonoBehaviour
	{
		private RectTransform _rect = null!;
		private Rect _lastSafeArea = new(0f, 0f, 0f, 0f);
		private Vector2Int _lastScreenSize = new(0, 0);

		private void Awake() => _rect = (RectTransform)transform;

		private void OnEnable() => Apply();

		private void Update()
		{
			var screenSize = new Vector2Int(Screen.width, Screen.height);

			if (Screen.safeArea == _lastSafeArea && screenSize == _lastScreenSize)
			{
				return;
			}

			Apply();
		}

		private void Apply()
		{
			var screenWidth = Screen.width;
			var screenHeight = Screen.height;

			// Guard against a zero-sized screen (can happen for a frame during startup/resize) — dividing by it
			// would yield NaN anchors and collapse the UI.
			if (screenWidth <= 0 || screenHeight <= 0)
			{
				return;
			}

			var safeArea = Screen.safeArea;

			var anchorMin = safeArea.position;
			var anchorMax = safeArea.position + safeArea.size;
			anchorMin.x /= screenWidth;
			anchorMin.y /= screenHeight;
			anchorMax.x /= screenWidth;
			anchorMax.y /= screenHeight;

			_rect.anchorMin = anchorMin;
			_rect.anchorMax = anchorMax;
			_rect.offsetMin = Vector2.zero;
			_rect.offsetMax = Vector2.zero;

			_lastSafeArea = safeArea;
			_lastScreenSize = new Vector2Int(screenWidth, screenHeight);
		}
	}
}
