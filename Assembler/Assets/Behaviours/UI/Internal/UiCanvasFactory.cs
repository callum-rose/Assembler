using UnityEngine;
using UnityEngine.UI;

namespace Assembler.Behaviours.UI.Internal
{
	/// <summary>
	/// Builds a screen-space-overlay <see cref="Canvas"/> (with a <see cref="CanvasScaler"/> that scales with
	/// screen size and a <see cref="GraphicRaycaster"/>) on a GameObject. Shared by the <c>ui canvas</c> behaviour
	/// and the on-screen-controls overlay so both stand up an identically-configured canvas.
	/// </summary>
	public static class UiCanvasFactory
	{
		public static Canvas AddOverlayCanvas(GameObject go, Vector2 referenceResolution, float matchWidthOrHeight)
		{
			var canvas = go.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;

			var scaler = go.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = referenceResolution;
			scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
			scaler.matchWidthOrHeight = Mathf.Clamp01(matchWidthOrHeight);

			go.AddComponent<GraphicRaycaster>();
			return canvas;
		}
	}
}
