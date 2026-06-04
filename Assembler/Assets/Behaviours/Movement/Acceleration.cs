using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Behaviours.Movement
{
	/// <summary>Integrates <c>Acceleration</c> into a velocity each frame.</summary>
	/// <remarks>
	/// Runs in one of two modes:
	///   • <b>Shared</b> — when <c>Velocity</c> points at a writable variable (e.g. <c>!var velocity</c>),
	///     the acceleration is integrated into that shared velocity and written back; position is left
	///     to a separate <c>velocity</c> integrator, so drag / speed-limit can compose on the same value.
	///   • <b>Standalone</b> — when <c>Velocity</c> is omitted, it integrates into a private velocity and
	///     moves the entity itself.
	/// Properties:
	///   Acceleration: World-space acceleration in units per second squared.
	///   Velocity [Vector3]: Optional shared velocity variable to integrate into (e.g. !var velocity).
	///     Omit for standalone mode where this behaviour also moves the entity.
	/// </remarks>
	public class Acceleration : GameBehaviour<AccelerationData>, INeedsGameClock
	{
		public IGameClock Clock { get; set; } = null!;

		private Vector3 _velocity;

		private void Update()
		{
			Execute(TriggerContext.Empty);
		}

		public override void Execute(TriggerContext ctx)
		{
			var dt = Clock.DeltaTime;

			if (Data.Velocity is not NullValueProvider<Vector3>)
			{
				// Shared mode: integrate into the shared velocity variable; leave position to the integrator.
				Data.Velocity.Set(Data.Velocity.Get(ctx) + Data.Acceleration.Get(ctx) * dt);
				return;
			}

			// Standalone mode: integrate a private velocity and move the entity directly.
			_velocity += Data.Acceleration.Get(ctx) * dt;
			transform.position += _velocity * dt;
		}
	}
}
