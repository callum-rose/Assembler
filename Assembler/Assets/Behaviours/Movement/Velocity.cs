using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;

namespace Assembler.Behaviours.Movement
{
	/// <summary>Moves the entity each frame by <c>Velocity * deltaTime</c>.</summary>
	/// <remarks>
	/// Doubles as the integrator for the shared-velocity system: point <c>Velocity</c> at a per-entity
	/// variable (<c>!var velocity</c>) and earlier behaviours — <c>acceleration</c>, <c>drag</c>,
	/// <c>speed limit</c> — mutate that same variable each frame before this reads it and moves the entity.
	/// Properties:
	///   Velocity: World-space velocity in units per second.
	/// </remarks>
	public class Velocity : GameBehaviour<VelocityData>, INeedsGameClock
	{
		public IGameClock Clock { get; set; } = null!;

		private void Update() => Step();

		internal void Step() => transform.position += Data.Velocity.Get() * Clock.DeltaTime;
	}
}
