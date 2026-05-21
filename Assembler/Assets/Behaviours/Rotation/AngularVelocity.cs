using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Rotation
{
	/// <summary>Rotates the entity each frame by <c>AngularVelocity * deltaTime</c> (Euler degrees per second).</summary>
	/// <remarks>
	/// Properties:
	///   AngularVelocity: World-space angular velocity in degrees per second (Euler per axis).
	/// </remarks>
	public class AngularVelocity : GameBehaviour<AngularVelocityData>
	{
		private void Update()
		{
			Execute();
		}

		public override void Execute()
		{
			transform.Rotate(Data.AngularVelocity.Value * Time.deltaTime, Space.World);
		}
	}
}
