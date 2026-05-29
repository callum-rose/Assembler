using Assembler.Behaviours.Triggers;
using Assembler.Parsing;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Debug.UI
{
	/// <summary>Draws a horizontal slider. Fires listeners whenever the slider value changes.</summary>
	/// <remarks>
	/// Properties:
	///   InitialValue: Starting slider value.
	///   MinValue: Minimum value the slider can produce.
	///   MaxValue: Maximum value the slider can produce.
	///   Rect: Screen-space rectangle.
	/// Outputs:
	///   value [float]: The new slider value after the change.
	/// </remarks>
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
			NotifyListeners(IncomingContext.With("value", _current));
		}
	}
}
