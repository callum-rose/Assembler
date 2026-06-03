using Assembler.Behaviours.UI.Internal;
using Assembler.Behaviours.UI.Views;
using Assembler.Behaviours.Triggers;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.UI
{
	/// <summary>A clickable uGUI button. Acts as a trigger: notifies its listeners each time it is
	/// clicked. The caption is re-read every frame, so it can be bound to a variable/expression.</summary>
	/// <remarks>
	/// Properties:
	///   Label: Button caption (re-read each frame).
	///   PreferredWidth: Preferred width for the parent layout (omit for a sensible default).
	///   PreferredHeight: Preferred height for the parent layout (omit for a sensible default).
	/// </remarks>
	public class UIButton : Trigger<UIButtonData>
	{
		// See TextLabel: the content is a stretch-to-fill child prefab, so fall back to a non-zero default
		// size when the descriptor omits a dimension to avoid the element collapsing to zero.
		private const float DefaultWidth = 160f;
		private const float DefaultHeight = 40f;

		private UiButtonView? _view;

		protected override void OnInitialise(UIButtonData data)
		{
			var host = UiLayout.EnsureRectTransform(gameObject);
			UiLayout.ApplyPreferredSize(gameObject,
				data.PreferredWidth.ValueOr(DefaultWidth),
				data.PreferredHeight.ValueOr(DefaultHeight));
			_view = UiLayout.InstantiateView<UiButtonView>(data.Prefab, host);
			_view.Button.onClick.AddListener(() => NotifyListeners(TriggerContext.Empty));
		}

		private void Update()
		{
			if (_view != null)
			{
				_view.Label.text = Data.Label.ValueOr(string.Empty);
			}
		}
	}
}
