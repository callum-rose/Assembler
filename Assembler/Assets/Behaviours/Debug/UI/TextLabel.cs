using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Debug.UI
{
	public class TextLabel : GameBehaviour<TextLabelData>
	{
		public override void Execute() { }

		private void OnGUI()
		{
			if (Data == null) return;
			var label = Data.Label.Value;
			var text = Data.Text.Value;
			var display = string.IsNullOrEmpty(label) ? text : label + text;
			GUI.Label(Data.Rect.ToUnityRect(), display);
		}
	}
}
