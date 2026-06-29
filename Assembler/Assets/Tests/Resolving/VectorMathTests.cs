using System;
using System.Collections.Generic;
using Assembler.Compiler.Compiler;
using Assembler.Libraries;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Resolving
{
	public class VectorMathTests
	{
		private const float Tol = 1e-4f;

		private static void AssertVec(Vector3 actual, float x, float y, float z = 0f)
		{
			Assert.That(actual.x, Is.EqualTo(x).Within(Tol), "x");
			Assert.That(actual.y, Is.EqualTo(y).Within(Tol), "y");
			Assert.That(actual.z, Is.EqualTo(z).Within(Tol), "z");
		}

		private static Vector3 V(float x, float y, float z = 0f) => new(x, y, z);

		[Test]
		public void ScaleVectorMultipliesEveryComponent()
		{
			AssertVec(VectorMath.ScaleVector(V(1, -2, 3), 2), 2, -4, 6);
		}

		[Test]
		public void AddAndSubtractVector()
		{
			AssertVec(VectorMath.AddVector(V(1, 2, 3), V(4, 5, 6)), 5, 7, 9);
			AssertVec(VectorMath.SubtractVector(V(4, 5, 6), V(1, 2, 3)), 3, 3, 3);
		}

		[Test]
		public void MagnitudeAndDistance()
		{
			Assert.That(VectorMath.Magnitude(V(3, 4)), Is.EqualTo(5f).Within(Tol));
			Assert.That(VectorMath.Distance(V(0, 0), V(3, 4)), Is.EqualTo(5f).Within(Tol));
		}

		[Test]
		public void NormalizeAndDirectionAreUnitLength()
		{
			AssertVec(VectorMath.Normalize(V(0, 5)), 0, 1);
			AssertVec(VectorMath.Direction(V(1, 1), V(1, 4)), 0, 1);
		}

		[Test]
		public void DotProduct()
		{
			Assert.That(VectorMath.Dot(V(1, 0), V(0, 1)), Is.EqualTo(0f).Within(Tol));
			Assert.That(VectorMath.Dot(V(2, 3), V(4, 5)), Is.EqualTo(23f).Within(Tol));
		}

		[Test]
		public void LerpVectorInterpolates()
		{
			AssertVec(VectorMath.LerpVector(V(0, 0), V(10, 20), 0.5f), 5, 10);
		}

		[Test]
		public void Rotate2DByNinetyDegrees()
		{
			// CCW 90 deg: (1, 0) -> (0, 1).
			AssertVec(VectorMath.Rotate2D(V(1, 0), 90), 0, 1);
			// CCW 180 deg: (1, 0) -> (-1, 0); z preserved.
			AssertVec(VectorMath.Rotate2D(V(1, 0, 7), 180), -1, 0, 7);
		}

		[Test]
		public void Angle2DMatchesAtan2()
		{
			Assert.That(VectorMath.Angle2D(V(1, 0)), Is.EqualTo(0f).Within(Tol));
			Assert.That(VectorMath.Angle2D(V(0, 1)), Is.EqualTo(90f).Within(Tol));
		}

		[Test]
		public void IntegratePositionWithExplicitDt()
		{
			AssertVec(VectorMath.IntegratePosition(V(1, 1), V(2, 4), 0.5f), 2, 3);
		}

		[Test]
		public void ForwardFromYawFacesPlusZThenPlusX()
		{
			// Yaw 0 faces +Z; yaw 90 faces +X — matching Quaternion.Euler(0, yaw, 0) * forward.
			AssertVec(VectorMath.ForwardFromYaw(0), 0, 0, 1);
			AssertVec(VectorMath.ForwardFromYaw(90), 1, 0, 0);
			AssertVec(VectorMath.ForwardFromYaw(180), 0, 0, -1);
		}

		[Test]
		public void RightFromYawIsNinetyClockwiseOfForward()
		{
			// Yaw 0: right is +X; yaw 90: right is -Z.
			AssertVec(VectorMath.RightFromYaw(0), 1, 0, 0);
			AssertVec(VectorMath.RightFromYaw(90), 0, 0, -1);
		}

		[Test]
		public void YawHelpersMatchUnityQuaternion()
		{
			foreach (var yaw in new[] { 17f, 123f, -48f })
			{
				var q = Quaternion.Euler(0f, yaw, 0f);
				var fwd = q * Vector3.forward;
				var right = q * Vector3.right;
				AssertVec(VectorMath.ForwardFromYaw(yaw), fwd.x, fwd.y, fwd.z);
				AssertVec(VectorMath.RightFromYaw(yaw), right.x, right.y, right.z);
			}
		}

		[Test]
		public void ForwardFromRotation2DFacesUpThenLeft()
		{
			// Rotation 0 faces +Y (up); 90 CCW faces -X; 180 faces -Y.
			AssertVec(VectorMath.ForwardFromRotation2D(0), 0, 1);
			AssertVec(VectorMath.ForwardFromRotation2D(90), -1, 0);
			AssertVec(VectorMath.ForwardFromRotation2D(180), 0, -1);
		}

		[Test]
		public void RightFromRotation2DIsNinetyClockwiseOfForward()
		{
			// Rotation 0: right is +X; 90: right is +Y.
			AssertVec(VectorMath.RightFromRotation2D(0), 1, 0);
			AssertVec(VectorMath.RightFromRotation2D(90), 0, 1);
		}

		[Test]
		public void Rotation2DHelpersMatchUnityQuaternion()
		{
			foreach (var degrees in new[] { 17f, 123f, -48f })
			{
				var q = Quaternion.Euler(0f, 0f, degrees);
				var fwd = q * Vector3.up;
				var right = q * Vector3.right;
				AssertVec(VectorMath.ForwardFromRotation2D(degrees), fwd.x, fwd.y, fwd.z);
				AssertVec(VectorMath.RightFromRotation2D(degrees), right.x, right.y, right.z);
			}
		}

		[Test]
		public void AnglesHelpersMatchUnityQuaternionBasis()
		{
			var euler = V(20, 45, 10);
			var q = Quaternion.Euler(euler);
			var fwd = q * Vector3.forward;
			var right = q * Vector3.right;
			var up = q * Vector3.up;
			AssertVec(VectorMath.ForwardFromAngles(euler), fwd.x, fwd.y, fwd.z);
			AssertVec(VectorMath.RightFromAngles(euler), right.x, right.y, right.z);
			AssertVec(VectorMath.UpFromAngles(euler), up.x, up.y, up.z);
		}

		// ---- compiler integration -------------------------------------------------

		private static CompiledExpressionsRegistry NewRegistry() =>
			new(BuiltInTypeRegistry.Default, new ExpressionMethodCompiler());

		private static ExpressionInfo Expr(string id, string returnType, string body,
			IReadOnlyList<(string type, string name)>? args = null) =>
			new(id, args ?? Array.Empty<(string, string)>(), returnType,
				Array.Empty<string>(), Array.Empty<string>(), body);

		[Test]
		public void ExpressionCanCallScaleVectorWithIntLiteral()
		{
			var registry = NewRegistry();
			registry.CompileAndRegisterAll(new[]
			{
				Expr("scaled", "vector", "return ScaleVector(v, 2);", new[] { ("vector", "v") }),
			});

			var func = (Func<Vector3, Vector3>)registry.GetCompiled("scaled").@delegate;
			AssertVec(func(V(1, -2, 3)), 2, -4, 6);
		}
	}
}
