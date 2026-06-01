using Assembler.Behaviours.Triggers;
using Assembler.Parsing;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Debug.UI
{
	/// <summary>Draws a text input field. Fires listeners when the user presses Enter to submit the typed text.</summary>
	/// <remarks>
	/// Properties:
	///   Rect: Screen-space rectangle.
	/// Outputs:
	///   text [string]: The submitted text. The field is cleared after submission.
	/// </remarks>
	public class UIInputField : Trigger<UIInputFieldData>
	{
		private string _text = string.Empty;
		private string _controlName;

		protected override void OnInitialise(UIInputFieldData data)
		{
			_controlName = "UIInputField_" + data.Id;
		}

		public override void Execute(TriggerContext ctx) { }

		private void OnGUI()
		{
			if (Data == null) return;
			GUI.SetNextControlName(_controlName);
			_text = GUI.TextField(Data.Rect.ToUnityRect(), _text);

			var e = Event.current;
			if (e.type == EventType.KeyDown
				&& (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
				&& GUI.GetNameOfFocusedControl() == _controlName)
			{
				var submitted = _text;
				_text = string.Empty;
				NotifyListeners(TriggerContext.New("text", submitted));
				e.Use();
			}
		}
	}
}
