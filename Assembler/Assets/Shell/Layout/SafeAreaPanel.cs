using UnityEngine;

namespace Assembler.Shell.Layout
{
	/// <summary>
	/// Pins this RectTransform to <see cref="Screen.safeArea"/>, so content inside it clears the notch, the home
	/// indicator and any rounded corners. Full-bleed decoration — the paper ground, the ink-dark header, the game
	/// strip's field — sits <em>outside</em> a panel like this and bleeds to the screen edge.
	/// </summary>
	/// <remarks>
	/// Anchors are normalised against the screen rather than the parent rect, which is correct as long as the
	/// panel's parent fills the canvas — every host under <see cref="ShellRoot"/> does.
	/// <para>
	/// It runs in edit mode, unlike <see cref="ShortAxisCanvasScaler"/>, because previewing the inset against a
	/// simulated device is the point of the component. The cost is that the anchors it writes land in the scene
	/// file, so switching device in the Device Simulator dirties the scene — harmless, since they are recomputed
	/// on enable, but it does show up in a diff.
	/// </para>
	/// </remarks>
	[ExecuteAlways]
	[DisallowMultipleComponent]
	[RequireComponent(typeof(RectTransform))]
	[AddComponentMenu("Assembler/Shell/Safe Area Panel")]
	public sealed class SafeAreaPanel : MonoBehaviour
	{
		private RectTransform? _rect;
		private Rect _appliedSafeArea;
		private Vector2Int _appliedScreenSize;

		private void OnEnable()
		{
			// Force the first Apply through: a zero rect never equals a real safe area.
			_appliedSafeArea = new Rect();
			_appliedScreenSize = Vector2Int.zero;
			Apply();
		}

		// Polled rather than event-driven: Unity raises no callback for a safe-area change, and the value shifts
		// on rotation, on a Device Simulator device swap and on an editor game-view resize.
		private void Update()
		{
			Apply();
		}

		/// <summary>Re-reads the safe area and re-anchors, if either it or the screen size has changed.</summary>
		public void Apply()
		{
			_rect = _rect != null ? _rect : GetComponent<RectTransform>();

			if (_rect == null)
			{
				return;
			}

			int screenWidth = Screen.width;
			int screenHeight = Screen.height;

			// A zero-sized screen happens for a frame during editor layout changes; anchoring against it would
			// divide by zero and collapse the panel.
			if (screenWidth <= 0 || screenHeight <= 0)
			{
				return;
			}

			var safeArea = Screen.safeArea;
			var screenSize = new Vector2Int(screenWidth, screenHeight);

			if (safeArea == _appliedSafeArea && screenSize == _appliedScreenSize)
			{
				return;
			}

			_appliedSafeArea = safeArea;
			_appliedScreenSize = screenSize;

			var min = new Vector2(safeArea.xMin / screenWidth, safeArea.yMin / screenHeight);
			var max = new Vector2(safeArea.xMax / screenWidth, safeArea.yMax / screenHeight);

			_rect.anchorMin = min;
			_rect.anchorMax = max;
			_rect.offsetMin = Vector2.zero;
			_rect.offsetMax = Vector2.zero;
		}
	}
}
