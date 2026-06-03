using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;
using UnityEngine.UI;

namespace Assembler.Behaviours.UI
{
	/// <summary>Roots a UI tree: adds a screen-space Canvas that scales with screen size. Place child UI
	/// entities (containers, labels, buttons) under this entity to compose the interface.</summary>
	/// <remarks>
	/// Properties:
	///   MatchWidthOrHeight: CanvasScaler match (0 = match width, 1 = match height, 0.5 = balanced).
	///   ReferenceResolution: Design resolution the UI scales from, as a vector (X = width, Y = height).
	/// </remarks>
	public class UICanvas : GameBehaviour<UICanvasData>
	{
		public override void Execute(TriggerContext ctx) { }

		protected override void OnInitialise(UICanvasData data)
		{
			var canvas = gameObject.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;

			var referenceResolution = data.ReferenceResolution.Get();

			var scaler = gameObject.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(referenceResolution.x, referenceResolution.y);
			scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
			scaler.matchWidthOrHeight = Mathf.Clamp01(data.MatchWidthOrHeight.Get());

			gameObject.AddComponent<GraphicRaycaster>();
		}
	}
}
