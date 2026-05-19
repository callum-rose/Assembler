using Assembler.Resolving.Behaviours;
using Assembler.Parsing;
using UnityEngine;

namespace Assembler.Behaviours.Debug.UI
{
	public class ProgressBar : GameBehaviour<ProgressBarData>
	{
		public override void Execute() { }

		private void OnGUI()
		{
			if (Data == null) return;
			var rect = Data.Rect.ToUnityRect();
			var t = Mathf.Clamp01(Data.Value.Value);
			GUI.Box(rect, GUIContent.none);
			GUI.Box(new Rect(rect.x, rect.y, rect.width * t, rect.height), GUIContent.none);
		}
	}
}
