using Assembler.Behaviours.Triggers;
using Assembler.Parsing;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Debug.UI
{
	public class UIInputField : Trigger<UIInputFieldData>
	{
		private string _text = string.Empty;
		private string _controlName;

		protected override void OnInitialise(UIInputFieldData data)
		{
			_controlName = "UIInputField_" + data.Id;
		}

		public override void Execute() { }

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
				TriggerContext.Push();
				try
				{
					TriggerContext.Set("text", (object)submitted);
					NotifyListeners();
				}
				finally
				{
					TriggerContext.Pop();
				}
				e.Use();
			}
		}
	}
}
