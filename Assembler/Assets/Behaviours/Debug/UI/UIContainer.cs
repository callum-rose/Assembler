using Assembler.Behaviours.Debug.UI.Internal;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;
using UnityEngine.UI;

namespace Assembler.Behaviours.Debug.UI
{
	/// <summary>Auto-layout container. Arranges its child UI entities in a vertical or horizontal stack
	/// using a uGUI layout group, so UIs reflow responsively without hand-placed coordinates.</summary>
	/// <remarks>
	/// Properties:
	///   Direction: "vertical" (default) or "horizontal".
	///   Spacing: Gap between children, in reference pixels.
	///   Padding: Inner padding on all sides, in reference pixels.
	///   ChildAlignment: e.g. "middle-center", "upper-left" (see TextAnchor names).
	///   FitContent: When true, the container shrinks to fit its children (adds a ContentSizeFitter).
	/// </remarks>
	public class UIContainer : GameBehaviour<UIContainerData>
	{
		public override void Execute(TriggerContext ctx) { }

		protected override void OnInitialise(UIContainerData data)
		{
			var rect = UiLayout.EnsureRectTransform(gameObject);

			// When sitting directly under a Canvas, fill the screen so children have room to lay out.
			if (transform.parent != null && transform.parent.GetComponent<Canvas>() != null)
			{
				UiLayout.StretchToFill(rect);
			}

			var horizontal = (data.Direction.Get() ?? string.Empty).Trim().ToLowerInvariant() == "horizontal";

			HorizontalOrVerticalLayoutGroup group = horizontal
				? gameObject.AddComponent<HorizontalLayoutGroup>()
				: gameObject.AddComponent<VerticalLayoutGroup>();

			var padding = Mathf.RoundToInt(data.Padding.Get());
			group.padding = new RectOffset(padding, padding, padding, padding);
			group.spacing = data.Spacing.Get();
			group.childAlignment = UiLayout.ParseAlignment(data.ChildAlignment.Get());
			group.childControlWidth = true;
			group.childControlHeight = true;
			group.childForceExpandWidth = false;
			group.childForceExpandHeight = false;

			if (data.FitContent.Get())
			{
				var fitter = gameObject.AddComponent<ContentSizeFitter>();
				fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
				fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			}
		}
	}
}
