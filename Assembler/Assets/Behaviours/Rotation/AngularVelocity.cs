using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Behaviours.Rotation
{
	/// <summary>Rotates the entity each frame by <c>AngularVelocity * deltaTime</c> (Euler degrees per second).</summary>
	/// <remarks>
	/// Properties:
	///   AngularVelocity: World-space angular velocity in degrees per second (Euler per axis).
	/// </remarks>
	public class AngularVelocity : GameBehaviour<AngularVelocityData>, INeedsGameClock
	{
		public IGameClock Clock { get; set; } = null!;

		private void Update()
		{
			Execute(TriggerContext.Empty);
		}

		public override void Execute(TriggerContext ctx)
		{
			transform.Rotate(Data.AngularVelocity.Get(ctx) * Clock.DeltaTime, Space.World);
		}
	}
}
