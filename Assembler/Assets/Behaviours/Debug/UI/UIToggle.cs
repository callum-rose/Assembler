using Assembler.Behaviours.Triggers;
using Assembler.Parsing;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Debug.UI
{
	/// <summary>Draws a toggle (checkbox). Fires listeners whenever the toggle's state changes.</summary>
	/// <remarks>
	/// Properties:
	///   InitialValue: Starting checked/unchecked state.
	///   Label: Caption shown next to the toggle.
	///   Rect: Screen-space rectangle.
	/// Outputs:
	///   value [bool]: The new toggle state after the change.
	/// </remarks>
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
			using (TriggerContext.Push())
			{
				TriggerContext.Set("value", (object)_current);
				NotifyListeners();
			}
		}
	}
}
