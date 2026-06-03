using Assembler.Behaviours.Debug.UI.Internal;
using Assembler.Behaviours.Debug.UI.Views;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Debug.UI
{
	/// <summary>Displays a line of text via a uGUI/TextMeshPro label. The text is re-read every frame, so
	/// binding it to a variable or expression shows live values (scores, timers, etc.).</summary>
	/// <remarks>
	/// Properties:
	///   Text: Body text (re-read each frame; bind to a variable/expression for live values).
	///   FontSize: Font size in reference pixels.
	///   PreferredWidth: Preferred width for the parent layout (&lt;= 0 = let layout decide).
	///   PreferredHeight: Preferred height for the parent layout (&lt;= 0 = let layout decide).
	/// </remarks>
	public class TextLabel : GameBehaviour<TextLabelData>
	{
		private UiLabelView? _view;

		public override void Execute(TriggerContext ctx) { }

		protected override void OnInitialise(TextLabelData data)
		{
			var host = UiLayout.EnsureRectTransform(gameObject);
			UiLayout.ApplyPreferredSize(gameObject, data.PreferredWidth.Get(), data.PreferredHeight.Get());
			_view = UiLayout.InstantiateView<UiLabelView>(data.Prefab, host);
		}

		private void Update()
		{
			if (_view == null) return;

			_view.Text.text = Data.Text.Get();

			var fontSize = Data.FontSize.Get();
			if (fontSize > 0)
			{
				_view.Text.fontSize = fontSize;
			}
		}
	}
}
