using Assembler.Behaviours.UI.Internal;
using Assembler.Behaviours.UI.Views;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.UI
{
	/// <summary>Displays a line of text via a uGUI/TextMeshPro label. The text is re-read every frame, so
	/// binding it to a variable or expression shows live values (scores, timers, etc.).</summary>
	/// <remarks>
	/// Properties:
	///   Text: Body text (re-read each frame; bind to a variable/expression for live values).
	///   FontSize: Font size in reference pixels.
	///   PreferredWidth: Preferred width for the parent layout (omit for a sensible default).
	///   PreferredHeight: Preferred height for the parent layout (omit for a sensible default).
	/// </remarks>
	public class TextLabel : GameBehaviour<TextLabelData>
	{
		// The visible content lives in a stretch-to-fill child prefab, so the entity itself reports no
		// intrinsic size to the parent layout group. Fall back to a non-zero default when the descriptor
		// omits a dimension, otherwise the element would collapse to zero.
		private const float DefaultWidth = 160f;
		private const float DefaultHeight = 30f;

		private UiLabelView? _view;

		protected override void OnInitialise(TextLabelData data)
		{
			var host = UiLayout.EnsureRectTransform(gameObject);
			UiLayout.ApplyPreferredSize(gameObject,
				data.PreferredWidth.ValueOr(DefaultWidth),
				data.PreferredHeight.ValueOr(DefaultHeight));
			_view = UiLayout.InstantiateView<UiLabelView>(data.Prefab, host);
		}

		private void Update()
		{
			if (_view == null) return;

			_view.Text.text = Data.Text.ValueOr(string.Empty);

			var fontSize = Data.FontSize.ValueOr(0);
			if (fontSize > 0)
			{
				_view.Text.fontSize = fontSize;
			}
		}
	}
}
