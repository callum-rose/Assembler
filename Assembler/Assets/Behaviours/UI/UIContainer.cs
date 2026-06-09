using Assembler.Behaviours.UI.Internal;
using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;
using UnityEngine.UI;

namespace Assembler.Behaviours.UI
{
	/// <summary>Groups child UI entities. By default it arranges them in a vertical or horizontal stack
	/// using a uGUI layout group so UIs reflow responsively without hand-placed coordinates; with
	/// Direction "none" it adds no layout group and children are positioned manually.</summary>
	/// <remarks>
	/// Properties:
	///   Direction: "vertical" (default), "horizontal", or "none"/"manual"/"free" (no layout group — manual placement).
	///   Spacing: Gap between children, in reference pixels (layout directions only).
	///   Padding: Inner padding on all sides, in reference pixels (layout directions only).
	///   ChildAlignment: e.g. "middle-center", "upper-left" (see TextAnchor names; layout directions only).
	///   FitContent: When true, the container shrinks to fit its children (adds a ContentSizeFitter).
	/// </remarks>
	public class UIContainer : GameBehaviour<UIContainerData>
	{
		protected override void OnInitialise(UIContainerData data)
		{
			var rect = UiLayout.EnsureRectTransform(gameObject);

			// When sitting directly under a Canvas, fill the screen so children have room to lay out.
			if (transform.parent != null && transform.parent.GetComponent<Canvas>() != null)
			{
				UiLayout.StretchToFill(rect);
			}

			var direction = data.Direction.ValueOr(LayoutDirection.Vertical);

			// None/Manual/Free: no layout group — children keep whatever position/anchors they declare.
			// Useful for absolute/overlay placement where auto-layout would get in the way.
			if (direction is LayoutDirection.None or LayoutDirection.Manual or LayoutDirection.Free)
			{
				return;
			}

			HorizontalOrVerticalLayoutGroup group = direction == LayoutDirection.Horizontal
				? gameObject.AddComponent<HorizontalLayoutGroup>()
				: gameObject.AddComponent<VerticalLayoutGroup>();

			var padding = (int)data.Padding.ValueOr(0f);
			group.padding = new RectOffset(padding, padding, padding, padding);
			group.spacing = data.Spacing.ValueOr(0f);
			group.childAlignment = data.ChildAlignment.ValueOr(TextAnchor.UpperCenter);
			group.childControlWidth = true;
			group.childControlHeight = true;
			group.childForceExpandWidth = false;
			group.childForceExpandHeight = false;

			if (data.FitContent.ValueOr(false))
			{
				var fitter = gameObject.AddComponent<ContentSizeFitter>();
				fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
				fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			}
		}
	}
}
