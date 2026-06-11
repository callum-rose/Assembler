using System.Collections.Generic;
using Assembler.Libraries;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Resolving
{
	public class SteeringMathTests
	{
		private const float Tol = 1e-4f;

		[Test]
		public void SeekPointsAtTargetAtMaxSpeed()
		{
			var v = SteeringMath.Seek(Vector3.zero, new Vector3(10, 0, 0), 3f);
			Assert.That(v.x, Is.EqualTo(3f).Within(Tol));
			Assert.That(v.y, Is.EqualTo(0f).Within(Tol));
		}

		[Test]
		public void FleePointsAwayFromTarget()
		{
			var v = SteeringMath.Flee(Vector3.zero, new Vector3(10, 0, 0), 3f);
			Assert.That(v.x, Is.EqualTo(-3f).Within(Tol));
		}

		[Test]
		public void ArriveSlowsInsideSlowingRadius()
		{
			// Half-way into the slowing radius -> half speed.
			var v = SteeringMath.Arrive(Vector3.zero, new Vector3(2, 0, 0), 4f, 4f);
			Assert.That(v.magnitude, Is.EqualTo(2f).Within(Tol));
		}

		[Test]
		public void ArriveIsZeroAtTarget()
		{
			var v = SteeringMath.Arrive(Vector3.zero, Vector3.zero, 4f, 4f);
			Assert.That(v.magnitude, Is.EqualTo(0f).Within(Tol));
		}

		[Test]
		public void PursueLeadsAMovingTarget()
		{
			// Target at x=4 moving +y; pursuing from origin at speed 4 (1s lead) aims ahead in +y.
			var v = SteeringMath.Pursue(Vector3.zero, new Vector3(4, 0, 0), new Vector3(0, 4, 0), 4f);
			Assert.Greater(v.y, 0f);
		}

		[Test]
		public void SeparatePushesAwayFromNeighbours()
		{
			var neighbours = new List<Vector3> { new(1, 0, 0) };
			var v = SteeringMath.Separate(Vector3.zero, neighbours, 5f, 3f);
			Assert.That(v.x, Is.EqualTo(-3f).Within(Tol));
		}

		[Test]
		public void SeparateIgnoresNeighboursOutsideRadius()
		{
			var neighbours = new List<Vector3> { new(50, 0, 0) };
			var v = SteeringMath.Separate(Vector3.zero, neighbours, 5f, 3f);
			Assert.That(v.magnitude, Is.EqualTo(0f).Within(Tol));
		}

		[Test]
		public void CohesionSteersTowardCentroid()
		{
			// Neighbours' centroid is at (2, 0); from origin that drives +x at max speed.
			var neighbours = new List<Vector3> { new(1, 0, 0), new(3, 0, 0) };
			var v = SteeringMath.Cohesion(Vector3.zero, neighbours, 3f);
			Assert.That(v.x, Is.EqualTo(3f).Within(Tol));
			Assert.That(v.y, Is.EqualTo(0f).Within(Tol));
		}

		[Test]
		public void CohesionWithSingleNeighbourSeeksIt()
		{
			var neighbours = new List<Vector3> { new(0, 4, 0) };
			var v = SteeringMath.Cohesion(Vector3.zero, neighbours, 2f);
			Assert.That(v.x, Is.EqualTo(0f).Within(Tol));
			Assert.That(v.y, Is.EqualTo(2f).Within(Tol));
		}

		[Test]
		public void CohesionWithNoNeighboursIsZero()
		{
			var v = SteeringMath.Cohesion(Vector3.zero, new List<Vector3>(), 3f);
			Assert.That(v.magnitude, Is.EqualTo(0f).Within(Tol));
		}

		[Test]
		public void AlignmentMatchesAverageHeadingAtMaxSpeed()
		{
			var velocities = new List<Vector3> { new(0, 2, 0), new(0, 4, 0) };
			var v = SteeringMath.Alignment(Vector3.zero, velocities, 5f);
			Assert.That(v.magnitude, Is.EqualTo(5f).Within(Tol));
			Assert.That(v.x, Is.EqualTo(0f).Within(Tol));
			Assert.Greater(v.y, 0f);
		}

		[Test]
		public void AlignmentWithNoNeighboursIsZero()
		{
			var v = SteeringMath.Alignment(Vector3.zero, new List<Vector3>(), 5f);
			Assert.That(v.magnitude, Is.EqualTo(0f).Within(Tol));
		}

		[Test]
		public void AlignmentWithCancellingVelocitiesIsZero()
		{
			var velocities = new List<Vector3> { new(0, 3, 0), new(0, -3, 0) };
			var v = SteeringMath.Alignment(Vector3.zero, velocities, 5f);
			Assert.That(v.magnitude, Is.EqualTo(0f).Within(Tol));
		}

		[Test]
		public void AvoidObstaclesBrakesAndSwervesForObstacleAhead()
		{
			// Heading +x; obstacle dead ahead, slightly to the +y side -> swerve to -y plus a braking -x component.
			var obstacles = new List<Vector3> { new(3, 0.5f, 0) };
			var v = SteeringMath.AvoidObstacles(Vector3.zero, new Vector3(2, 0, 0), obstacles, 6f, 2f, 4f);

			Assert.Less(v.x, 0f, "should brake against the heading");
			Assert.Less(v.y, 0f, "should swerve away from the obstacle's side");
			Assert.That(v.magnitude, Is.LessThanOrEqualTo(4f + Tol));
		}

		[Test]
		public void AvoidObstaclesIgnoresObstacleBehind()
		{
			var obstacles = new List<Vector3> { new(-3, 0, 0) };
			var v = SteeringMath.AvoidObstacles(Vector3.zero, new Vector3(2, 0, 0), obstacles, 6f, 2f, 4f);
			Assert.That(v.magnitude, Is.EqualTo(0f).Within(Tol));
		}

		[Test]
		public void AvoidObstaclesIgnoresObstacleOutsideCorridor()
		{
			// Ahead in x, but far off the heading line (beyond avoidRadius) -> clears the agent.
			var obstacles = new List<Vector3> { new(3, 50, 0) };
			var v = SteeringMath.AvoidObstacles(Vector3.zero, new Vector3(2, 0, 0), obstacles, 6f, 2f, 4f);
			Assert.That(v.magnitude, Is.EqualTo(0f).Within(Tol));
		}

		[Test]
		public void AvoidObstaclesIgnoresObstacleBeyondLookAhead()
		{
			var obstacles = new List<Vector3> { new(50, 0, 0) };
			var v = SteeringMath.AvoidObstacles(Vector3.zero, new Vector3(2, 0, 0), obstacles, 6f, 2f, 4f);
			Assert.That(v.magnitude, Is.EqualTo(0f).Within(Tol));
		}

		[Test]
		public void AvoidObstaclesGrowsAsObstacleCloses()
		{
			var near = new List<Vector3> { new(1, 0.5f, 0) };
			var far = new List<Vector3> { new(5, 0.5f, 0) };
			var vNear = SteeringMath.AvoidObstacles(Vector3.zero, new Vector3(2, 0, 0), near, 6f, 2f, 4f);
			var vFar = SteeringMath.AvoidObstacles(Vector3.zero, new Vector3(2, 0, 0), far, 6f, 2f, 4f);
			Assert.Greater(vNear.magnitude, vFar.magnitude);
		}

		[Test]
		public void AvoidObstaclesIsZeroWhenStationary()
		{
			var obstacles = new List<Vector3> { new(3, 0, 0) };
			var v = SteeringMath.AvoidObstacles(Vector3.zero, Vector3.zero, obstacles, 6f, 2f, 4f);
			Assert.That(v.magnitude, Is.EqualTo(0f).Within(Tol));
		}

		[Test]
		public void Heading2DMeasuresFromPositiveX()
		{
			Assert.That(SteeringMath.Heading2D(Vector3.zero, new Vector3(0, 1, 0)), Is.EqualTo(90f).Within(Tol));
		}

		[Test]
		public void LookRotation2DPutsHeadingOnZ()
		{
			var euler = SteeringMath.LookRotation2D(Vector3.zero, new Vector3(0, 1, 0));
			Assert.That(euler.z, Is.EqualTo(90f).Within(Tol));
		}

		[Test]
		public void YawFromDirectionXZFacesPlusZAsZero()
		{
			Assert.That(SteeringMath.YawFromDirectionXZ(new Vector3(0, 0, 1)), Is.EqualTo(0f).Within(Tol));
		}

		[Test]
		public void YawFromDirectionXZFacesPlusXAsNinety()
		{
			Assert.That(SteeringMath.YawFromDirectionXZ(new Vector3(1, 0, 0)), Is.EqualTo(90f).Within(Tol));
		}

		[Test]
		public void YawFromDirectionXZFacesMinusXAsMinusNinety()
		{
			Assert.That(SteeringMath.YawFromDirectionXZ(new Vector3(-1, 0, 0)), Is.EqualTo(-90f).Within(Tol));
		}

		[Test]
		public void YawFromDirectionXZIgnoresYComponent()
		{
			// A steep vertical offset must not perturb the yaw: only x/z determine the heading.
			Assert.That(SteeringMath.YawFromDirectionXZ(new Vector3(1, 99, 0)), Is.EqualTo(90f).Within(Tol));
		}

		[Test]
		public void YawFromDirectionXZInvertsTheForwardConvention()
		{
			// forward = (sin yaw, cos yaw) in XZ; recovering yaw from that forward must round-trip.
			const float yaw = 37f;
			var rad = yaw * Mathf.Deg2Rad;
			var forward = new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad));
			Assert.That(SteeringMath.YawFromDirectionXZ(forward), Is.EqualTo(yaw).Within(Tol));
		}

		[Test]
		public void LookRotationXZPutsHeadingOnYWithFlatXAndZ()
		{
			var euler = SteeringMath.LookRotationXZ(Vector3.zero, new Vector3(1, 0, 0));
			Assert.That(euler.x, Is.EqualTo(0f).Within(Tol));
			Assert.That(euler.y, Is.EqualTo(90f).Within(Tol));
			Assert.That(euler.z, Is.EqualTo(0f).Within(Tol));
		}
	}
}
