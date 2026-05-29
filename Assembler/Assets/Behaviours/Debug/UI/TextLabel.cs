using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Parsing;
using UnityEngine;

namespace Assembler.Behaviours.Debug.UI
{
	/// <summary>Draws a text label on-screen using IMGUI. Useful for debug HUD and scoreboards.</summary>
	/// <remarks>
	/// Properties:
	///   Text: Dynamic body text (re-read each frame; bind to a variable to display live values).
	///   Label: Optional static prefix shown before Text (e.g. "Score: ").
	///   FontSize: Font size in pixels.
	///   Rect: Screen-space rectangle (see ScreenRect format).
	/// </remarks>
	public class TextLabel : GameBehaviour<TextLabelData>
	{
		private GUIStyle _style;

		public override void Execute(TriggerContext ctx) { }

		private void OnGUI()
		{
			if (Data == null) return;
			_style ??= new GUIStyle(GUI.skin.label);
			_style.fontSize = Data.FontSize.Get(TriggerContext.Empty);
			var label = Data.Label.Get(TriggerContext.Empty);
			var text = Data.Text.Get(TriggerContext.Empty);
			var display = string.IsNullOrEmpty(label) ? text : label + text;
			GUI.Label(Data.Rect.ToUnityRect(), display, _style);
		}
	}
}
