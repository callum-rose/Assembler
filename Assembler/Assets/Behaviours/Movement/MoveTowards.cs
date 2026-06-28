using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Movement
{
	/// <summary>Moves the entity toward <c>Target</c> at a constant <c>Speed</c>, never overshooting.</summary>
	/// <remarks>
	/// Properties:
	///   Target: World-space position to move toward.
	///   Speed: Movement speed in units per second; a step never passes the target.
	/// </remarks>
	public class MoveTowards : PerFrameBehaviour<MoveTowardsData>
	{
		internal override void Step()
		{
			transform.position = Vector3.MoveTowards(
				transform.position, Data.Target.Get(), Data.Speed.Get() * Clock.DeltaTime);
		}
	}
}
