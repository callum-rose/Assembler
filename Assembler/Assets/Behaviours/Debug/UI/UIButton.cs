using Assembler.Behaviours.Triggers;
using Assembler.Parsing;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Debug.UI
{
	/// <summary>Draws a button. Acts as a trigger: notifies listeners each time the button is clicked.</summary>
	/// <remarks>
	/// Properties:
	///   Label: Button caption.
	///   Rect: Screen-space rectangle.
	/// </remarks>
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
