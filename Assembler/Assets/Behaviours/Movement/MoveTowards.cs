using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Behaviours.Movement
{
	/// <summary>Moves the entity toward <c>Target</c> at a constant <c>Speed</c>, never overshooting.</summary>
	/// <remarks>
	/// Properties:
	///   Target: World-space position to move toward.
	///   Speed: Movement speed in units per second; a step never passes the target.
	/// </remarks>
	public class MoveTowards : GameBehaviour<MoveTowardsData>, INeedsGameClock
	{
		public IGameClock Clock { get; set; } = null!;

		private void Update() => Step();

		internal void Step()
		{
			transform.position = Vector3.MoveTowards(
				transform.position, Data.Target.Get(), Data.Speed.Get() * Clock.DeltaTime);
		}
	}
}
