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
	///   PreferredWidth: Preferred width for the parent layout (omit to let the layout decide).
	///   PreferredHeight: Preferred height for the parent layout (omit to let the layout decide).
	/// </remarks>
	public class UIButton : Trigger<UIButtonData>
	{
		private UiButtonView? _view;

		protected override void OnInitialise(UIButtonData data)
		{
			var host = UiLayout.EnsureRectTransform(gameObject);
			UiLayout.ApplyPreferredSize(gameObject, data.PreferredWidth.ValueOr(0f), data.PreferredHeight.ValueOr(0f));
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
