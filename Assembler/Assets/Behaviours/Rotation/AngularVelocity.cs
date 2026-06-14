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
	public class AngularVelocity : PerFrameBehaviour<AngularVelocityData>
	{
		internal override void Step() =>
			transform.Rotate(Data.AngularVelocity.Get() * Clock.DeltaTime, Space.World);
	}
}
