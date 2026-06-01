using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Behaviours.Movement
{
	/// <summary>Integrates <c>Acceleration</c> into an internal velocity each frame, then moves the entity by that velocity.</summary>
	/// <remarks>
	/// Properties:
	///   Acceleration: World-space acceleration in units per second squared.
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
			_velocity += Data.Acceleration.Get(ctx) * dt;
			transform.position += _velocity * dt;
		}
	}
}
