using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Movement
{
	/// <summary>Constrains the entity's position to the axis-aligned box between <c>Min</c> and <c>Max</c> each frame.</summary>
	/// <remarks>
	/// Each axis is clamped independently, so an entity driven past an edge stops at the boundary.
	/// Frame-rate independent, so it needs no clock.
	/// Properties:
	///   Min: Lower per-axis bound of the allowed region.
	///   Max: Upper per-axis bound of the allowed region.
	/// </remarks>
	public class ClampPosition : GameBehaviour<ClampPositionData>
	{
		private void Update()
		{
			Execute(TriggerContext.Empty);
		}

		public override void Execute(TriggerContext ctx)
		{
			var min = Data.Min.Get(ctx);
			var max = Data.Max.Get(ctx);
			var p = transform.position;
			transform.position = new Vector3(
				Mathf.Clamp(p.x, min.x, max.x),
				Mathf.Clamp(p.y, min.y, max.y),
				Mathf.Clamp(p.z, min.z, max.z));
		}
	}
}
