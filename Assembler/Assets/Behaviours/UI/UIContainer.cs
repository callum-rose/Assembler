using Assembler.Behaviours.UI.Internal;
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
	///   Direction: "vertical" (default), "horizontal", or "none" (no layout group — manual placement).
	///   Spacing: Gap between children, in reference pixels (layout directions only).
	///   Padding: Inner padding on all sides, in reference pixels (layout directions only).
	///   ChildAlignment: e.g. "middle-center", "upper-left" (see TextAnchor names; layout directions only).
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

			var direction = (data.Direction.Get() ?? string.Empty).Trim().ToLowerInvariant();

			// "none" (or "manual"/"free"): no layout group — children keep whatever position/anchors they
			// declare. Useful for absolute/overlay placement where auto-layout would get in the way.
			if (direction is "none" or "manual" or "free")
			{
				return;
			}

			HorizontalOrVerticalLayoutGroup group = direction == "horizontal"
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

			data.FitContent.UseIfValueExists(fitContent =>
			{
				if (fitContent)
				{
					var fitter = gameObject.AddComponent<ContentSizeFitter>();
					fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
					fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
				}
			});
		}
	}
}
