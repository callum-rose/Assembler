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
	public class NumberMathTests
	{
		private const float Tol = 1e-4f;

		[Test]
		public void ClampConstrainsToRange()
		{
			Assert.That(NumberMath.Clamp(5, 0, 10), Is.EqualTo(5f).Within(Tol));
			Assert.That(NumberMath.Clamp(-3, 0, 10), Is.EqualTo(0f).Within(Tol));
			Assert.That(NumberMath.Clamp(15, 0, 10), Is.EqualTo(10f).Within(Tol));
			Assert.That(NumberMath.Clamp01(1.5f), Is.EqualTo(1f).Within(Tol));
		}

		[Test]
		public void MinMaxAbsSign()
		{
			Assert.That(NumberMath.Min(3, 7), Is.EqualTo(3f).Within(Tol));
			Assert.That(NumberMath.Max(3, 7), Is.EqualTo(7f).Within(Tol));
			Assert.That(NumberMath.Abs(-4), Is.EqualTo(4f).Within(Tol));
			Assert.That(NumberMath.Sign(-2), Is.EqualTo(-1f).Within(Tol));
			Assert.That(NumberMath.Sign(0), Is.EqualTo(0f).Within(Tol));
			Assert.That(NumberMath.Sign(2), Is.EqualTo(1f).Within(Tol));
		}

		[Test]
		public void RoundFloorCeil()
		{
			Assert.That(NumberMath.Round(2.4f), Is.EqualTo(2f).Within(Tol));
			Assert.That(NumberMath.Round(2.6f), Is.EqualTo(3f).Within(Tol));
			Assert.That(NumberMath.Floor(2.9f), Is.EqualTo(2f).Within(Tol));
			Assert.That(NumberMath.Ceil(2.1f), Is.EqualTo(3f).Within(Tol));
		}

		[Test]
		public void LerpAndRemap()
		{
			Assert.That(NumberMath.Lerp(0, 10, 0.25f), Is.EqualTo(2.5f).Within(Tol));
			// Map 5 from [0,10] into [0,100] -> 50.
			Assert.That(NumberMath.Remap(5, 0, 10, 0, 100), Is.EqualTo(50f).Within(Tol));
			// Map -1 from [-1,1] into [0,1] -> 0.
			Assert.That(NumberMath.Remap(-1, -1, 1, 0, 1), Is.EqualTo(0f).Within(Tol));
		}

		[Test]
		public void DegreeRadianRoundTrip()
		{
			Assert.That(NumberMath.DegToRad(180), Is.EqualTo(Mathf.PI).Within(Tol));
			Assert.That(NumberMath.RadToDeg(Mathf.PI), Is.EqualTo(180f).Within(Tol));
		}

		[Test]
		public void Approx()
		{
			Assert.That(NumberMath.Approx(1f, 1f + 1e-7f), Is.True);
			Assert.That(NumberMath.Approx(1f, 1.5f), Is.False);
		}

		// ---- compiler integration -------------------------------------------------

		private static CompiledExpressionsRegistry NewRegistry() =>
			new(BuiltInTypeRegistry.Default, new ExpressionMethodCompiler());

		private static ExpressionInfo Expr(string id, string returnType, string body,
			IReadOnlyList<(string type, string name)>? args = null) =>
			new(id, args ?? Array.Empty<(string, string)>(), returnType,
				Array.Empty<string>(), Array.Empty<string>(), body);

		[Test]
		public void ExpressionCoercesIntArgsToFloatClamp()
		{
			var registry = NewRegistry();
			registry.CompileAndRegisterAll(new[]
			{
				// x is an int arg, 0/10 are int literals — all coerce to float params.
				Expr("clamped", "float", "return Clamp(x, 0, 10);", new[] { ("int", "x") }),
			});

			var func = (Func<int, float>)registry.GetCompiled("clamped").@delegate;
			Assert.That(func(5), Is.EqualTo(5f).Within(Tol));
			Assert.That(func(-3), Is.EqualTo(0f).Within(Tol));
			Assert.That(func(99), Is.EqualTo(10f).Within(Tol));
		}
	}
}
