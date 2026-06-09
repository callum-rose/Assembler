using System;
using System.Collections.Generic;
using Assembler.Compiler.Compiler;
using Assembler.Libraries;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using NUnit.Framework;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Tests.Resolving
{
	public class RandomMathTests
	{
		private const float Tol = 1e-3f;

		[SetUp]
		public void Seed() => Random.InitState(12345);

		[Test]
		public void RandomFloatStaysInRange()
		{
			for (int i = 0; i < 100; i++)
			{
				float v = RandomMath.RandomFloat(2f, 5f);
				Assert.That(v, Is.GreaterThanOrEqualTo(2f).And.LessThanOrEqualTo(5f));
			}
		}

		[Test]
		public void RandomIntIsInclusiveRange()
		{
			for (int i = 0; i < 100; i++)
			{
				int v = RandomMath.RandomInt(1, 3);
				Assert.That(v, Is.InRange(1, 3));
			}
		}

		[Test]
		public void ChanceBounds()
		{
			Assert.That(RandomMath.Chance(0f), Is.False, "p=0 never true");
			Assert.That(RandomMath.Chance(1f), Is.True, "p=1 always true");
		}

		[Test]
		public void RandomOnCircleHasRequestedRadius()
		{
			for (int i = 0; i < 100; i++)
			{
				Vector3 p = RandomMath.RandomOnCircle(4f);
				Assert.That(p.magnitude, Is.EqualTo(4f).Within(Tol));
				Assert.That(p.z, Is.EqualTo(0f).Within(Tol));
			}
		}

		[Test]
		public void RandomInsideCircleStaysInside()
		{
			for (int i = 0; i < 100; i++)
			{
				Vector3 p = RandomMath.RandomInsideCircle(4f);
				Assert.That(p.magnitude, Is.LessThanOrEqualTo(4f + Tol));
			}
		}

		[Test]
		public void RandomColorIsOpaqueAndInRange()
		{
			Color c = RandomMath.RandomColor();
			Assert.That(c.a, Is.EqualTo(1f).Within(Tol));
			Assert.That(c.r, Is.InRange(0f, 1f));
			Assert.That(c.g, Is.InRange(0f, 1f));
			Assert.That(c.b, Is.InRange(0f, 1f));
		}

		[Test]
		public void PickReturnsAListElement()
		{
			var items = new List<Vector3> { new(1, 0, 0), new(2, 0, 0), new(3, 0, 0) };
			for (int i = 0; i < 50; i++)
			{
				Assert.That(items, Does.Contain(RandomMath.Pick(items)));
			}

			var ints = new List<int> { 7, 8, 9 };
			for (int i = 0; i < 50; i++)
			{
				Assert.That(ints, Does.Contain(RandomMath.PickInt(ints)));
			}
		}

		[Test]
		public void WeightedPickIndexStaysInRange()
		{
			var weights = new List<float> { 1f, 2f, 3f };
			for (int i = 0; i < 100; i++)
			{
				Assert.That(RandomMath.WeightedPickIndex(weights), Is.InRange(0, weights.Count - 1));
			}
		}

		[Test]
		public void WeightedPickIndexOnlyReturnsPositivelyWeightedEntries()
		{
			// Only index 1 has positive weight, so it must always be chosen.
			var weights = new List<float> { 0f, 1f, 0f };
			for (int i = 0; i < 100; i++)
			{
				Assert.That(RandomMath.WeightedPickIndex(weights), Is.EqualTo(1));
			}
		}

		[Test]
		public void WeightedPickIndexFallsBackToUniformWhenAllZero()
		{
			var weights = new List<float> { 0f, 0f, 0f };
			for (int i = 0; i < 100; i++)
			{
				Assert.That(RandomMath.WeightedPickIndex(weights), Is.InRange(0, weights.Count - 1));
			}
		}

		// ---- compiler integration -------------------------------------------------

		private static CompiledExpressionsRegistry NewRegistry() =>
			new(BuiltInTypeRegistry.Default, new ExpressionMethodCompiler());

		private static ExpressionInfo Expr(string id, string returnType, string body,
			IReadOnlyList<(string type, string name)>? args = null) =>
			new(id, args ?? Array.Empty<(string, string)>(), returnType,
				Array.Empty<string>(), Array.Empty<string>(), body);

		[Test]
		public void ExpressionCanCallRandomOnCircleWithIntLiteral()
		{
			var registry = NewRegistry();
			registry.CompileAndRegisterAll(new[]
			{
				Expr("spawn", "vector", "return RandomOnCircle(5);"),
			});

			var func = (Func<Vector3>)registry.GetCompiled("spawn").@delegate;
			Assert.That(func().magnitude, Is.EqualTo(5f).Within(Tol));
		}
	}
}
