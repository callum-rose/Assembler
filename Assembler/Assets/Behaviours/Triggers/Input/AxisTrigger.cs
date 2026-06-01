using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Input
{
	/// <summary>Fires every frame with the current value(s) of one or two Unity input axes (1D or 2D).</summary>
	/// <remarks>
	/// Properties:
	///   XAxis: Name of the Unity input axis read into the x component (e.g. "Horizontal").
	///   YAxis: Optional. Name of the Unity input axis read into the y component (e.g. "Vertical"). Leave unset for a 1D axis.
	/// Outputs:
	///   axis [Vector2]: Combined (x, y) axis value; y is 0 when YAxis is unset.
	///   x [float]: Current XAxis value.
	///   y [float]: Current YAxis value, or 0 when YAxis is unset.
	/// </remarks>
	public class AxisTrigger : InputTrigger<AxisTriggerData>
	{
		private void Update()
		{
			if (InputBoundary.ReplayActive)
			{
				return;
			}

			var xName = Data.XAxis.Get();
			var yName = Data.YAxis.ValueOr(string.Empty);

			var x = string.IsNullOrEmpty(xName) ? 0f : UnityEngine.Input.GetAxis(xName);
			var y = string.IsNullOrEmpty(yName) ? 0f : UnityEngine.Input.GetAxis(yName);

			FireInput(TriggerContext.New(b =>
			{
				b["axis"] = new Vector2(x, y);
				b["x"] = x;
				b["y"] = y;
			}));
		}
	}
}
