using Assembler.Resolving.Behaviours;
using Assembler.Parsing;
using UnityEngine;

namespace Assembler.Behaviours.Debug.UI
{
	public class TextLabel : GameBehaviour<TextLabelData>
	{
		private GUIStyle _style;

		public override void Execute() { }

		private void OnGUI()
		{
			if (Data == null) return;
			_style ??= new GUIStyle(GUI.skin.label);
			_style.fontSize = Data.FontSize.Value;
			var label = Data.Label.Value;
			var text = Data.Text.Value;
			var display = string.IsNullOrEmpty(label) ? text : label + text;
			GUI.Label(Data.Rect.ToUnityRect(), display, _style);
		}
	}
}
