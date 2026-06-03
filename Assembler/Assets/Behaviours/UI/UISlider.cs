using Assembler.Behaviours.UI.Internal;
using Assembler.Behaviours.UI.Views;
using Assembler.Behaviours.Triggers;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.UI
{
	/// <summary>A uGUI slider. Acts as a trigger: notifies its listeners whenever the value changes.</summary>
	/// <remarks>
	/// Properties:
	///   InitialValue: Starting value.
	///   MinValue: Minimum value the slider can produce.
	///   MaxValue: Maximum value the slider can produce.
	///   PreferredWidth: Preferred width for the parent layout (omit to let the layout decide).
	///   PreferredHeight: Preferred height for the parent layout (omit to let the layout decide).
	/// Outputs:
	///   value [float]: The new slider value after the change.
	/// </remarks>
	public class UISlider : Trigger<UISliderData>
	{
		private UiSliderView? _view;

		protected override void OnInitialise(UISliderData data)
		{
			var host = UiLayout.EnsureRectTransform(gameObject);
			UiLayout.ApplyPreferredSize(gameObject, data.PreferredWidth, data.PreferredHeight);
			_view = UiLayout.InstantiateView<UiSliderView>(data.Prefab, host);

			var slider = _view.Slider;
			slider.minValue = data.MinValue.ValueOr(0f);
			slider.maxValue = data.MaxValue.ValueOr(1f);
			slider.SetValueWithoutNotify(data.InitialValue.ValueOr(0f));
			slider.onValueChanged.AddListener(value => NotifyListeners(TriggerContext.New("value", value)));
		}
	}
}
