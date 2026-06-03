using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public class UIButtonData : TriggerData
	{
		public IValueProvider<string> Label { get; }
		public IValueProvider<float> PreferredWidth { get; }
		public IValueProvider<float> PreferredHeight { get; }

		/// <summary>The uGUI prefab (carrying a UiButtonView) instantiated for this button.</summary>
		public GameObject Prefab { get; }

		public UIButtonData(string id,
			IValueProvider<string> label,
			IValueProvider<float> preferredWidth,
			IValueProvider<float> preferredHeight,
			GameObject prefab) : base(id) =>
			(Label, PreferredWidth, PreferredHeight, Prefab) = (label, preferredWidth, preferredHeight, prefab);
	}
}
