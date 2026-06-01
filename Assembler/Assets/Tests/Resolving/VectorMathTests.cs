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
