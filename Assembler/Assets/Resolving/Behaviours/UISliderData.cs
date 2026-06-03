using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public class UISliderData : TriggerData
	{
		public IValueProvider<float> InitialValue { get; }
		public IValueProvider<float> MinValue { get; }
		public IValueProvider<float> MaxValue { get; }
		public IValueProvider<float> PreferredWidth { get; }
		public IValueProvider<float> PreferredHeight { get; }

		/// <summary>The uGUI prefab (carrying a UiSliderView) instantiated for this slider.</summary>
		public GameObject Prefab { get; }

		public UISliderData(string id,
			IValueProvider<float> initialValue,
			IValueProvider<float> minValue,
			IValueProvider<float> maxValue,
			IValueProvider<float> preferredWidth,
			IValueProvider<float> preferredHeight,
			GameObject prefab) : base(id) =>
			(InitialValue, MinValue, MaxValue, PreferredWidth, PreferredHeight, Prefab) =
				(initialValue, minValue, maxValue, preferredWidth, preferredHeight, prefab);
	}
}
