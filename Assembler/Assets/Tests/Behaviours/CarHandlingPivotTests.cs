using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	// Regression guard for the MiniRacer3D car's cornering geometry. The car's `drive velocity`
	// expression is meant to turn the car about its REAR AXLE (nose sweeps wide, tail tracks) rather
	// than spin about its centre. This replicates the control loop against real PhysX and asserts the
	// pivot really lands behind the centre — and documents why the naive "retained sideways slip" drift
	// model was rejected (it pivots about the FRONT, i.e. understeers, in this set-velocity model).
	//
	// "pivotS" is the longitudinal position (in body-forward units from the centre) of the point with
	// zero sideways velocity: negative => rear of centre (rear-axle pivot), positive => front of centre.
	public class CarHandlingPivotTests
	{
		private const float Dt = 0.02f;
		private const int Steps = 250;
		private const float TurnRate = 2.4f;
		private const float GripSpeed = 3f;
		private const float Top = 9f;
		private const float Accel = 2.5f;
		private const float B = 1.0f; // rear axle offset, matches the descriptor's `rear axle offset`

		[Test]
		public void KinematicModel_PivotsAboutRearAxle()
		{
			var pivotS = SimulatePivot(kinematic: true);
			Assert.That(pivotS, Is.LessThan(-0.25f),
				$"Expected a rear-axle pivot (pivotS < 0) but got pivotS={pivotS:F2}. " +
				"drive velocity must set lateral = angVel.y * b (pure kinematic).");
		}

		[Test]
		public void RetainedSlipDriftModel_PivotsAboutFront_AndIsRejected()
		{
			// Documents the trade-off: a low-grip retained-slip drift understeers (front pivot) in this
			// model, which is why drive velocity uses the pure kinematic term instead.
			var pivotS = SimulatePivot(kinematic: false);
			Assert.That(pivotS, Is.GreaterThan(0.25f),
				$"Expected the retained-slip model to pivot front (pivotS > 0) but got pivotS={pivotS:F2}.");
		}

		// Returns the steady-state pivot position (body-forward units from centre) after a full-lock turn.
		private static float SimulatePivot(bool kinematic)
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
					var omega = 1f * TurnRate * authority * dir; // steer = +1 (full lock)
					rb.angularVelocity = new Vector3(0f, omega, 0f);

					var next = Mathf.Lerp(current, Top, Mathf.Clamp01(Accel * Dt));
					var lateral = kinematic
						? omega * B
						: Mathf.Lerp(Vector3.Dot(vel, right), 0f, Mathf.Clamp01(1f * Dt)); // grip=1 drift
					rb.linearVelocity = forward * next + right * lateral;
					Physics.Simulate(Dt);
				}

				var rgt = go.transform.right;
				var omegaFinal = rb.angularVelocity.y;
				var centreLat = Vector3.Dot(rb.GetPointVelocity(go.transform.position), rgt);
				return Mathf.Abs(omegaFinal) > 1e-4f ? -centreLat / omegaFinal : 0f;
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
