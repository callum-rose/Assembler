using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public class TextLabelData : BehaviourData
	{
		public IValueProvider<string> Text { get; }
		public IValueProvider<int> FontSize { get; }
		public IValueProvider<float> PreferredWidth { get; }
		public IValueProvider<float> PreferredHeight { get; }

		/// <summary>The uGUI prefab (carrying a UiLabelView) instantiated for this label.</summary>
		public GameObject Prefab { get; }

		public TextLabelData(string id,
			IValueProvider<string> text,
			IValueProvider<int> fontSize,
			IValueProvider<float> preferredWidth,
			IValueProvider<float> preferredHeight,
			GameObject prefab) : base(id) =>
			(Text, FontSize, PreferredWidth, PreferredHeight, Prefab) =
				(text, fontSize, preferredWidth, preferredHeight, prefab);
	}
}
