using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;

namespace Assembler.Behaviours.Movement
{
	/// <summary>Moves the entity each frame by <c>Velocity * deltaTime</c>.</summary>
	/// <remarks>
	/// Properties:
	///   Velocity: World-space velocity in units per second.
	/// </remarks>
	public class Velocity : GameBehaviour<VelocityData>, INeedsGameClock
	{
		public IGameClock Clock { get; set; } = null!;

		private void Update()
		{
			Execute(TriggerContext.Empty);
		}

		public override void Execute(TriggerContext ctx)
		{
			transform.position += Data.Velocity.Get(ctx) * Clock.DeltaTime;
		}
	}
}