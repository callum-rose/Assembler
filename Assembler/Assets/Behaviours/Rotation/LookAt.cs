using Assembler.Libraries;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Behaviours.Rotation
{
	/// <summary>Turns the entity each frame to face <c>Target</c> in the XZ ground plane (a yaw about +Y).</summary>
	/// <remarks>
	/// The declarative form of the <c>LookRotationXZ</c> library helper, for 3D ground-plane games (Y up,
	/// movement on XZ). Only the yaw is driven — the entity's pitch/roll (Euler X/Z) are left untouched, so it
	/// composes with a tilt. A flat-zero offset (the target sits directly above/below) leaves the rotation as-is.
	/// Properties:
	///   Target: World-space point to face.
	///   TurnRate: Maximum turn speed in degrees/sec; 0 (the default) snaps instantly to face the target.
	/// </remarks>
	public sealed class LookAt : GameBehaviour<LookAtData>, INeedsGameClock
	{
		public IGameClock Clock { get; set; } = null!;

		private void Update()
		{
			var offset = Data.Target.Get() - transform.position;

			// Nothing to face if the target is directly above/below (no XZ heading); keep the current rotation.
			if (offset.x * offset.x + offset.z * offset.z < 1e-8f)
			{
				return;
			}

			var desiredYaw = SteeringMath.YawFromDirectionXZ(offset);
			var euler = transform.eulerAngles;
			var turnRate = Data.TurnRate.Get();
			var yaw = turnRate > 0f
				? Mathf.MoveTowardsAngle(euler.y, desiredYaw, turnRate * Clock.DeltaTime)
				: desiredYaw;

			transform.eulerAngles = new Vector3(euler.x, yaw, euler.z);
		}
	}
}
