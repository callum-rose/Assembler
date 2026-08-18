using UnityEngine;
using UnityEngine.UI;

namespace Assembler.Shell.Layout
{
	/// <summary>
	/// Keeps the canvas's <em>short</em> axis at a fixed number of units whichever way the device is held, by
	/// swapping the <see cref="CanvasScaler"/>'s reference resolution and match axis on rotation.
	/// </summary>
	/// <remarks>
	/// The shell itself is locked portrait, so this only earns its keep once a landscape <em>game</em> unlocks
	/// autorotation at play time — at which point the only live hierarchies are the game strip and the overlays,
	/// and both are authored against a 390-unit short axis. Without the swap, a landscape screen matched on
	/// height would map the long axis to 390 and everything would double in size.
	/// </remarks>
	/// <remarks>
	/// Play mode only, deliberately — no <c>[ExecuteAlways]</c>. This writes the reference resolution, and the
	/// reference resolution is an authored design constant; running in edit mode would overwrite the authored
	/// 390 × 844 with whatever aspect the game view happened to be, and save that into the scene.
	/// </remarks>
	[DisallowMultipleComponent]
	[RequireComponent(typeof(CanvasScaler))]
	[AddComponentMenu("Assembler/Shell/Short Axis Canvas Scaler")]
	public sealed class ShortAxisCanvasScaler : MonoBehaviour
	{
		[Tooltip("Units across the short edge of the screen — the width in portrait, the height in landscape.")]
		[SerializeField] private float shortAxisUnits = 390f;

		[Tooltip("Units along the long edge. Only the short axis is guaranteed; this sets the design aspect.")]
		[SerializeField] private float longAxisUnits = 844f;

		private CanvasScaler? _scaler;
		private bool _appliedPortrait;
		private bool _hasApplied;

		private void OnEnable()
		{
			_hasApplied = false;
			Apply();
		}

		// Polled because there is no orientation-changed callback; the check is a pair of int comparisons.
		private void Update()
		{
			Apply();
		}

		/// <summary>Re-reads the screen's orientation and re-points the scaler, if it has changed.</summary>
		public void Apply()
		{
			_scaler = _scaler != null ? _scaler : GetComponent<CanvasScaler>();

			if (_scaler == null || Screen.width <= 0 || Screen.height <= 0)
			{
				return;
			}

			bool portrait = Screen.height >= Screen.width;

			if (_hasApplied && portrait == _appliedPortrait)
			{
				return;
			}

			_hasApplied = true;
			_appliedPortrait = portrait;

			_scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			_scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

			// Vector2 is forced here by the CanvasScaler API.
			_scaler.referenceResolution = portrait
				? new Vector2(shortAxisUnits, longAxisUnits)
				: new Vector2(longAxisUnits, shortAxisUnits);

			// 0 matches width, 1 matches height — either way, the short axis.
			_scaler.matchWidthOrHeight = portrait ? 0f : 1f;
		}
	}
}
