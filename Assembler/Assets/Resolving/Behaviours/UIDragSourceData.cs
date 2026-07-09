using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class UIDragSourceData : TriggerData
	{
		public IValueProvider<string> Label { get; }
		public IValueProvider<float> PreferredWidth { get; }
		public IValueProvider<float> PreferredHeight { get; }

		/// <summary>The uGUI prefab (carrying a UiButtonView) instantiated for this drag source.</summary>
		public GameObject Prefab { get; }

		public UIDragSourceData(string id,
			IValueProvider<string> label,
			IValueProvider<float> preferredWidth,
			IValueProvider<float> preferredHeight,
			GameObject prefab) : base(id) =>
			(Label, PreferredWidth, PreferredHeight, Prefab) = (label, preferredWidth, preferredHeight, prefab);
	}
}
