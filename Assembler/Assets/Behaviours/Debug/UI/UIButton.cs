using Assembler.Behaviours.Triggers;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Debug.UI
{
	public class UIButton : Trigger<UIButtonData>
	{
		public override void Execute() { }

		private void OnGUI()
		{
			if (Data == null) return;
			if (GUI.Button(Data.Rect.ToUnityRect(), Data.Label.Value))
				NotifyListeners();
		}
	}
}
