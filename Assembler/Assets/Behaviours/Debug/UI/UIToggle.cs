using Assembler.Behaviours.Triggers;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Debug.UI
{
	public class UIToggle : Trigger<UIToggleData>
	{
		private bool _current;

		protected override void OnInitialise(UIToggleData data)
		{
			_current = data.InitialValue.Value;
		}

		public override void Execute() { }

		private void OnGUI()
		{
			if (Data == null) return;
			var next = GUI.Toggle(Data.Rect.ToUnityRect(), _current, Data.Label.Value);
			if (next == _current) return;

			_current = next;
			TriggerContext.Push();
			try
			{
				TriggerContext.Set("value", (object)_current);
				NotifyListeners();
			}
			finally
			{
				TriggerContext.Pop();
			}
		}
	}
}
