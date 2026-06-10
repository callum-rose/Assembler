using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	// Regression guard for the MiniRacer3D car's cornering + drift, replicating the `drive velocity`
	// break-loose model against real PhysX. The car carves about its REAR AXLE (nose sweeps wide, tail
	// tracks) while the rear grips, and breaks into a drift only when cornering demand exceeds the rear
	// tyres' grip limit. Two regimes are asserted:
	//   * gentle cornering  -> rear grips, pivots behind the centre (rear-axle carve)
	//   * hard cornering     -> the rear breaks loose into a sustained sideways slide (drift)
	//
	// "pivotS" is the longitudinal position (body-forward units from the centre) of the zero-sideways-
	// velocity point: negative => rear of centre. "residualSlip" is the rear's sideways slide beyond the
	// kinematic carve (≈0 when gripping, large when drifting).
	public class CarHandlingPivotTests
	{
		private const float Dt = 0.02f;
		private const int Steps = 250;
		private const float TurnRate = 2.4f;
		private const float GripSpeed = 3f;
		private const float Top = 9f;
		private const float Accel = 2.5f;
		private const float B = 1.0f;       // matches the descriptor's `rear axle offset`
		private const float RearGrip = 14f; // matches the descriptor's `rear grip`

		[Test]
		public void GentleCornering_RearGrips_PivotsAboutRearAxle()
		{
			var (pivotS, residualSlip) = Simulate(steer: 0.4f);
			Assert.That(Mathf.Abs(residualSlip), Is.LessThan(0.5f),
				$"Gentle cornering should keep the rear gripped (slip ~ 0) but slip={residualSlip:F2}.");
			Assert.That(pivotS, Is.LessThan(-0.25f),
				$"Gentle cornering should pivot about the rear axle (pivotS < 0) but pivotS={pivotS:F2}.");
		}

		[Test]
		public void HardCornering_RearBreaksLooseIntoDrift()
		{
			var (_, residualSlip) = Simulate(steer: 1.0f);
			Assert.That(Mathf.Abs(residualSlip), Is.GreaterThan(1.5f),
				$"Hard cornering should break the rear loose into a drift (large slip) but slip={residualSlip:F2}.");
		}

		// Drives a sustained turn at the given steer and returns (pivotS, residualSlip) at steady state.
		private static (float pivotS, float residualSlip) Simulate(float steer)
		{
			var prevMode = Physics.simulationMode;
			Physics.simulationMode = SimulationMode.Script;
			var go = new GameObject("handling-test-car");
			try
			{
				go.transform.position = new Vector3(0f, 0.35f, 0f);
				go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

				var bc = go.AddComponent<BoxCollider>();
				bc.size = new Vector3(1f, 0.5f, 2f);

				var rb = go.AddComponent<Rigidbody>();
				rb.useGravity = false;
				rb.mass = 1f;
				rb.linearDamping = 0f;
				rb.angularDamping = 0f;
				rb.constraints = RigidbodyConstraints.FreezePositionY |
					RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
				rb.linearVelocity = go.transform.forward * 5f;

				for (var i = 0; i < Steps; i++)
				{
					var theta = go.transform.eulerAngles.y;
					var heading = Rotate2D(new Vector3(0f, 1f, 0f), -theta);
					var forward = new Vector3(heading.x, 0f, heading.y);
					var right = new Vector3(forward.z, 0f, -forward.x);
					var vel = rb.linearVelocity;

					var current = Vector3.Dot(vel, forward);
					var authority = Mathf.Clamp01(vel.magnitude / GripSpeed);
					var dir = current < 0f ? -1f : 1f;
					var omega = steer * TurnRate * authority * dir;
					rb.angularVelocity = new Vector3(0f, omega, 0f);

					var next = Mathf.Lerp(current, Top, Mathf.Clamp01(Accel * Dt));

					// Break-loose lateral: kinematic rear-track + force-limited slip recovery.
					var kin = omega * B;
					var rearSlip = Vector3.Dot(vel, right) - kin;
					var maxDelta = RearGrip * Dt;
					var newSlip = rearSlip - Mathf.Clamp(rearSlip, -maxDelta, maxDelta);
					rb.linearVelocity = forward * next + right * (newSlip + kin);
					Physics.Simulate(Dt);
				}

				var rgt = go.transform.right;
				var omegaFinal = rb.angularVelocity.y;
				var centreLat = Vector3.Dot(rb.GetPointVelocity(go.transform.position), rgt);
				var pivotS = Mathf.Abs(omegaFinal) > 1e-4f ? -centreLat / omegaFinal : 0f;
				var residualSlip = Vector3.Dot(rb.linearVelocity, rgt) - omegaFinal * B;
				return (pivotS, residualSlip);
			}
			finally
			{
				Object.DestroyImmediate(go);
				Physics.simulationMode = prevMode;
			}
		}

		private static Vector3 Rotate2D(Vector3 v, float degrees)
		{
			var rad = degrees * Mathf.Deg2Rad;
			var c = Mathf.Cos(rad);
			var s = Mathf.Sin(rad);
			return new Vector3(v.x * c - v.y * s, v.x * s + v.y * c, v.z);
		}
	}
}
