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
	}
}
