using Assembler.Behaviours.Triggers;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Debug.UI
{
	public class UISlider : Trigger<UISliderData>
	{
		private float _current;

		protected override void OnInitialise(UISliderData data)
		{
			_current = data.InitialValue.Value;
		}

		public override void Execute() { }

		private void OnGUI()
		{
			if (Data == null) return;
			var next = GUI.HorizontalSlider(Data.Rect.ToUnityRect(), _current, Data.MinValue.Value, Data.MaxValue.Value);
			if (Mathf.Approximately(next, _current)) return;

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
