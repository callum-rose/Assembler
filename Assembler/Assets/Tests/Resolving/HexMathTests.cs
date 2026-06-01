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
	public class HexMathTests
	{
		private const float Tol = 1e-3f;

		private static void AssertVec(Vector3 actual, float x, float y, float z = 0f)
		{
			Assert.That(actual.x, Is.EqualTo(x).Within(Tol), "x");
			Assert.That(actual.y, Is.EqualTo(y).Within(Tol), "y");
			Assert.That(actual.z, Is.EqualTo(z).Within(Tol), "z");
		}

		private static Vector3 Hex(float q, float r) => new(q, r, 0f);

		[Test]
		public void HexToWorldPointyOriginAndUnit()
		{
			AssertVec(HexMath.HexToWorldPointy(Hex(0, 0), 1f), 0, 0);
			// q = 1, r = 0 -> x = sqrt(3) * size, y = 0.
			AssertVec(HexMath.HexToWorldPointy(Hex(1, 0), 1f), Mathf.Sqrt(3f), 0);
		}

		[Test]
		public void HexToWorldFlatOriginAndUnit()
		{
			AssertVec(HexMath.HexToWorldFlat(Hex(0, 0), 1f), 0, 0);
			// q = 1, r = 0 -> x = 1.5 * size, y = sqrt(3)/2 * size.
			AssertVec(HexMath.HexToWorldFlat(Hex(1, 0), 1f), 1.5f, Mathf.Sqrt(3f) / 2f);
		}

		[Test]
		public void HexDistanceCountsSteps()
		{
			Assert.That(HexMath.HexDistance(Hex(0, 0), Hex(0, 0)), Is.EqualTo(0f).Within(Tol));
			Assert.That(HexMath.HexDistance(Hex(0, 0), Hex(3, 0)), Is.EqualTo(3f).Within(Tol));
			Assert.That(HexMath.HexDistance(Hex(0, 0), Hex(-1, -1)), Is.EqualTo(2f).Within(Tol));
		}

		[Test]
		public void HexNeighbourFollowsDirectionTable()
		{
			AssertVec(HexMath.HexNeighbour(Hex(0, 0), 0), 1, 0);
			AssertVec(HexMath.HexNeighbour(Hex(0, 0), 2), 0, -1);
			// Index wraps mod 6.
			AssertVec(HexMath.HexNeighbour(Hex(0, 0), 6), 1, 0);
			// Every neighbour is exactly one step away.
			for (int d = 0; d < 6; d++)
			{
				Assert.That(HexMath.HexDistance(Hex(2, 2), HexMath.HexNeighbour(Hex(2, 2), d)),
					Is.EqualTo(1f).Within(Tol));
			}
		}

		[Test]
		public void HexNeighboursReturnsSixDistinctCells()
		{
			var neighbours = HexMath.HexNeighbours(Hex(0, 0));
			Assert.That(neighbours.Count, Is.EqualTo(6));
			foreach (var n in neighbours)
			{
				Assert.That(HexMath.HexDistance(Hex(0, 0), n), Is.EqualTo(1f).Within(Tol));
			}
		}

		[Test]
		public void HexRoundSnapsToNearestCell()
		{
			AssertVec(HexMath.HexRound(Hex(2, 1)), 2, 1);
			AssertVec(HexMath.HexRound(Hex(0.1f, 0.1f)), 0, 0);
			AssertVec(HexMath.HexRound(Hex(2.9f, -0.1f)), 3, 0);
		}

		// ---- compiler integration -------------------------------------------------

		private static CompiledExpressionsRegistry NewRegistry() =>
			new(BuiltInTypeRegistry.Default, new ExpressionMethodCompiler());

		private static ExpressionInfo Expr(string id, string returnType, string body,
			IReadOnlyList<(string type, string name)>? args = null) =>
			new(id, args ?? Array.Empty<(string, string)>(), returnType,
				Array.Empty<string>(), Array.Empty<string>(), body);

		[Test]
		public void ExpressionCanCallHexNeighbourWithIntLiteral()
		{
			var registry = NewRegistry();
			registry.CompileAndRegisterAll(new[]
			{
				Expr("step", "vector", "return HexNeighbour(h, 0);", new[] { ("vector", "h") }),
			});

			var func = (Func<Vector3, Vector3>)registry.GetCompiled("step").@delegate;
			AssertVec(func(Hex(0, 0)), 1, 0);
		}
	}
}
