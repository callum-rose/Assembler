using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Rotation
{
	/// <summary>Adds Displacement (Euler degrees) to the entity's world rotation each time it Executes (e.g. via a trigger).</summary>
	/// <remarks>
	/// Properties:
	///   Displacement: World-space Euler angle offset (degrees) to add on each execution.
	/// </remarks>
	public class Rotate : GameBehaviour<RotateData>, IAmExecutable
	{
		public void Execute(TriggerContext ctx)
		{
			transform.Rotate(Data.Displacement.Get(ctx), Space.World);
		}
	}
}
