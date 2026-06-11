using Assembler.Behaviours.UI.Internal;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

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
		protected override void OnInitialise(UICanvasData data)
		{
			var referenceResolution = data.ReferenceResolution.ValueOr(new Vector3(1920f, 1080f, 0f));

			UiCanvasFactory.AddOverlayCanvas(
				gameObject,
				new Vector2(referenceResolution.x, referenceResolution.y),
				data.MatchWidthOrHeight.ValueOr(0.5f));
		}
	}
}
