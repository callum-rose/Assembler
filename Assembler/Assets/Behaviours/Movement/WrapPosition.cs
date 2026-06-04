using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Movement
{
	/// <summary>Wraps the entity's position around the box between <c>Min</c> and <c>Max</c> each frame (toroidal screen-wrap).</summary>
	/// <remarks>
	/// Per axis, a position below <c>Min</c> reappears at <c>Max</c> and one above <c>Max</c> reappears at
	/// <c>Min</c> — the classic Asteroids wrap. Frame-rate independent, so it needs no clock.
	/// Properties:
	///   Min: Lower per-axis bound; crossing it teleports the entity to the matching Max edge.
	///   Max: Upper per-axis bound; crossing it teleports the entity to the matching Min edge.
	/// </remarks>
	public class WrapPosition : GameBehaviour<WrapPositionData>
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
				Wrap(p.x, min.x, max.x),
				Wrap(p.y, min.y, max.y),
				Wrap(p.z, min.z, max.z));
		}

		private static float Wrap(float value, float min, float max)
		{
			if (value < min)
			{
				return max;
			}

			if (value > max)
			{
				return min;
			}

			return value;
		}
	}
}
