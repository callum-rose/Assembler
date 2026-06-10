using Assembler.Libraries;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Resolving
{
	public class LayoutMathTests
	{
		private const float Tol = 1e-4f;

		[Test]
		public void GridPositionsAreRowMajorFromOrigin()
		{
			var grid = LayoutMath.GridPositions(3, 2, 0.5f, new Vector3(1, 1, 0));

			Assert.AreEqual(6, grid.Count);
			// Row 0 first (y = 1), then row 1 (y = 1.5); columns step by cellSize from origin.x.
			CollectionAssert.AreEqual(
				new[]
				{
					new Vector3(1f, 1f, 0f), new Vector3(1.5f, 1f, 0f), new Vector3(2f, 1f, 0f),
					new Vector3(1f, 1.5f, 0f), new Vector3(1.5f, 1.5f, 0f), new Vector3(2f, 1.5f, 0f),
				},
				grid);
		}

		[Test]
		public void GridPositionsZeroDimensionIsEmpty()
		{
			Assert.AreEqual(0, LayoutMath.GridPositions(0, 5, 1f, Vector3.zero).Count);
			Assert.AreEqual(0, LayoutMath.GridPositions(5, 0, 1f, Vector3.zero).Count);
		}

		[Test]
		public void LinePositionsIncludeBothEndpoints()
		{
			var line = LayoutMath.LinePositions(new Vector3(0, 0, 0), new Vector3(4, 0, 0), 5);

			Assert.AreEqual(5, line.Count);
			CollectionAssert.AreEqual(
				new[]
				{
					new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(2, 0, 0),
					new Vector3(3, 0, 0), new Vector3(4, 0, 0),
				},
				line);
		}

		[Test]
		public void LinePositionsCountOneYieldsStart()
		{
			var line = LayoutMath.LinePositions(new Vector3(2, 3, 0), new Vector3(9, 9, 0), 1);
			Assert.AreEqual(1, line.Count);
			Assert.AreEqual(new Vector3(2, 3, 0), line[0]);
		}

		[Test]
		public void RingPositionsAreEvenlySpacedFromPlusX()
		{
			var ring = LayoutMath.RingPositions(new Vector3(0, 0, 0), 2f, 4);

			Assert.AreEqual(4, ring.Count);
			AssertApprox(ring[0], new Vector3(2, 0, 0));
			AssertApprox(ring[1], new Vector3(0, 2, 0));
			AssertApprox(ring[2], new Vector3(-2, 0, 0));
			AssertApprox(ring[3], new Vector3(0, -2, 0));
		}

		[Test]
		public void PositionListAddAndToListRoundTrips()
		{
			var builder = new PositionList();
			builder.Add(new Vector3(1, 2, 3));
			builder.Add(new Vector3(4, 5, 6));

			var list = builder.ToList();

			CollectionAssert.AreEqual(new[] { new Vector3(1, 2, 3), new Vector3(4, 5, 6) }, list);

			// ToList returns a fresh copy: mutating the result doesn't disturb the builder.
			list.Clear();
			Assert.AreEqual(2, builder.ToList().Count);
		}

		private static void AssertApprox(Vector3 actual, Vector3 expected)
		{
			Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tol), "x");
			Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tol), "y");
			Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tol), "z");
		}
	}
}
