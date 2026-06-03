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
	///   PreferredWidth: Preferred width for the parent layout (&lt;= 0 = let layout decide).
	///   PreferredHeight: Preferred height for the parent layout (&lt;= 0 = let layout decide).
	/// Outputs:
	///   value [float]: The new slider value after the change.
	/// </remarks>
	public class UISlider : Trigger<UISliderData>
	{
		private UiSliderView? _view;

		public override void Execute(TriggerContext ctx) { }

		protected override void OnInitialise(UISliderData data)
		{
			var host = UiLayout.EnsureRectTransform(gameObject);
			UiLayout.ApplyPreferredSize(gameObject, data.PreferredWidth.Get(), data.PreferredHeight.Get());
			_view = UiLayout.InstantiateView<UiSliderView>(data.Prefab, host);

			var slider = _view.Slider;
			slider.minValue = data.MinValue.Get();
			slider.maxValue = data.MaxValue.Get();
			slider.SetValueWithoutNotify(data.InitialValue.Get());
			slider.onValueChanged.AddListener(value => NotifyListeners(TriggerContext.New("value", value)));
		}
	}
}
