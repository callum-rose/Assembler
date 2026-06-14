using System;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Movement
{
	/// <summary>Clamps a shared velocity variable's magnitude to <c>Max</c> each frame.</summary>
	/// <remarks>
	/// Layers onto the shared-velocity system: it reads and rewrites the same <c>!var velocity</c> that an
	/// <c>acceleration</c> feeds and a <c>velocity</c> integrator consumes. Clamping is frame-rate
	/// independent, so it ignores the clock's delta — but still pauses with the game; direction is preserved.
	/// Properties:
	///   Velocity [Vector3]: Writable shared velocity variable to clamp (required, e.g. !var velocity).
	///   Max: Maximum allowed speed (magnitude) in units per second.
	/// </remarks>
	public class SpeedLimit : PerFrameBehaviour<SpeedLimitData>
	{
		protected override void OnInitialise(SpeedLimitData data)
		{
			if (data.Velocity is NullValueProvider<Vector3>)
			{
				throw new InvalidOperationException(
					$"speed limit behaviour '{data.Id}' requires a writable Velocity variable, e.g. !var velocity.");
			}
		}

		internal override void Step() =>
			Data.Velocity.Set(Vector3.ClampMagnitude(Data.Velocity.Get(), Data.Max.Get()));
	}
}
