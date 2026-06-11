using Assembler.Behaviours.UI;
using Assembler.Behaviours.UI.Internal;
using Assembler.Behaviours.UI.Views;
using Assembler.Input;
using Assembler.Parsing.Controls;
using UnityEngine;

namespace Assembler.Building
{
	/// <summary>
	/// Renders the descriptor's <c>Controls.OnScreen</c> widgets into a screen-space overlay at build time, but
	/// only when the active platform is Mobile. Each widget is just another physical input source: it synthesises
	/// into the control path its action is bound to under <c>mobile</c> (see <see cref="OnScreenControlPaths"/>),
	/// which the live <c>InputActionAsset</c> already reads — so this is pure rendering/wiring, no input plumbing.
	/// </summary>
	public static class OnScreenControlsBuilder
	{
		// Overlay canvas defaults — a 1080p design space with a balanced width/height match, matching ui canvas.
		private static readonly Vector2 ReferenceResolution = new(1920f, 1080f);
		private const float MatchWidthOrHeight = 0.5f;

		public static void Build(ControlsInfo controls, InputPlatform activePlatform, UiPrefabLibrary prefabs, Transform gameRoot)
		{
			if (activePlatform != InputPlatform.Mobile || controls.OnScreen.Count == 0)
			{
				return;
			}

			// Build the canvas inactive so each widget's On-Screen component doesn't enable (and wire its control)
			// until after we've set its controlPath; activating the canvas at the end fires every OnEnable once
			// the paths are in place. Avoids the InputSystem 1.11.2 caveat that controlPath only takes effect on enable.
			var canvasGo = new GameObject("OnScreenControls", typeof(RectTransform));
			canvasGo.transform.SetParent(gameRoot, worldPositionStays: false);
			canvasGo.SetActive(false);

			UiCanvasFactory.AddOverlayCanvas(canvasGo, ReferenceResolution, MatchWidthOrHeight);

			// Anchor widgets to a safe-area rect rather than the raw canvas, so corner-pinned controls (e.g. a
			// lower-left joystick) stay clear of notches/cutouts on mobile. Off mobile the safe area is the
			// full screen, so this is transparent — though this overlay only builds on mobile anyway.
			var safeAreaGo = new GameObject("SafeArea", typeof(RectTransform));
			safeAreaGo.transform.SetParent(canvasGo.transform, worldPositionStays: false);
			UiLayout.StretchToSafeArea((RectTransform)safeAreaGo.transform);

			foreach (var control in controls.OnScreen)
			{
				BuildWidget(control, controls, prefabs, safeAreaGo.transform);
			}

			canvasGo.SetActive(true);
		}

		private static void BuildWidget(OnScreenControlInfo control, ControlsInfo controls, UiPrefabLibrary prefabs, Transform canvas)
		{
			// Validation already guarantees a single simple mobile path; guard anyway rather than render a dead widget.
			if (!OnScreenControlPaths.TryResolvePath(controls, control, out var path))
			{
				return;
			}

			var prefab = control.Kind switch
			{
				OnScreenControlKind.Joystick => prefabs.JoystickPrefab,
				OnScreenControlKind.DPad => prefabs.DPadPrefab,
				OnScreenControlKind.Button => prefabs.MobileButtonPrefab,
				_ => null
			};

			if (prefab == null)
			{
				return;
			}

			var instance = Object.Instantiate(prefab, canvas, worldPositionStays: false);
			ApplyAnchorLayout((RectTransform)instance.transform, control.Anchor, control.Offset, control.Size);

			switch (control.Kind)
			{
				case OnScreenControlKind.Joystick:
					instance.GetComponent<OnScreenJoystickView>().Bind(path);
					break;

				case OnScreenControlKind.DPad:
					instance.GetComponent<OnScreenDPadView>().Bind(path);
					break;

				case OnScreenControlKind.Button:
					var button = instance.GetComponent<OnScreenButtonView>();
					button.Bind(path);
					button.SetLabel(control.Label ?? string.Empty);
					break;
			}
		}

		// Anchors the widget to one corner/edge of the canvas and offsets it inward from there. The offset's sign
		// follows the anchor so a positive Offset always pushes the widget toward the screen centre.
		private static void ApplyAnchorLayout(RectTransform rect, TextAnchor anchor, Vector3 offset, Vector3 size)
		{
			var anchorPoint = AnchorPoint(anchor);
			rect.anchorMin = anchorPoint;
			rect.anchorMax = anchorPoint;
			rect.pivot = anchorPoint;
			rect.sizeDelta = new Vector2(size.x, size.y);

			var dirX = anchorPoint.x == 0f ? 1f : anchorPoint.x == 1f ? -1f : 0f;
			var dirY = anchorPoint.y == 0f ? 1f : anchorPoint.y == 1f ? -1f : 0f;
			rect.anchoredPosition = new Vector2(offset.x * dirX, offset.y * dirY);
		}

		private static Vector2 AnchorPoint(TextAnchor anchor) => anchor switch
		{
			TextAnchor.UpperLeft => new Vector2(0f, 1f),
			TextAnchor.UpperCenter => new Vector2(0.5f, 1f),
			TextAnchor.UpperRight => new Vector2(1f, 1f),
			TextAnchor.MiddleLeft => new Vector2(0f, 0.5f),
			TextAnchor.MiddleCenter => new Vector2(0.5f, 0.5f),
			TextAnchor.MiddleRight => new Vector2(1f, 0.5f),
			TextAnchor.LowerLeft => new Vector2(0f, 0f),
			TextAnchor.LowerCenter => new Vector2(0.5f, 0f),
			TextAnchor.LowerRight => new Vector2(1f, 0f),
			_ => new Vector2(0.5f, 0.5f)
		};
	}
}
