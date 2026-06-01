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
	public class ColorMathTests
	{
		private const float Tol = 1e-3f;

		private static void AssertColor(Color actual, float r, float g, float b, float a)
		{
			Assert.That(actual.r, Is.EqualTo(r).Within(Tol), "r");
			Assert.That(actual.g, Is.EqualTo(g).Within(Tol), "g");
			Assert.That(actual.b, Is.EqualTo(b).Within(Tol), "b");
			Assert.That(actual.a, Is.EqualTo(a).Within(Tol), "a");
		}

		[Test]
		public void LerpColorBlendsHalfway()
		{
			AssertColor(ColorMath.LerpColor(Color.black, Color.white, 0.5f), 0.5f, 0.5f, 0.5f, 1f);
		}

		[Test]
		public void WithAlphaReplacesOnlyAlpha()
		{
			AssertColor(ColorMath.WithAlpha(new Color(0.2f, 0.4f, 0.6f, 1f), 0.25f), 0.2f, 0.4f, 0.6f, 0.25f);
		}

		[Test]
		public void BrightenAndDarkenMoveTowardWhiteAndBlack()
		{
			var c = new Color(0.5f, 0.5f, 0.5f, 0.8f);
			AssertColor(ColorMath.Brighten(c, 1f), 1f, 1f, 1f, 0.8f);
			AssertColor(ColorMath.Darken(c, 1f), 0f, 0f, 0f, 0.8f);
			// factor 0 is a no-op.
			AssertColor(ColorMath.Brighten(c, 0f), 0.5f, 0.5f, 0.5f, 0.8f);
		}

		[Test]
		public void GrayscaleIsUniformAcrossChannels()
		{
			var g = ColorMath.Grayscale(new Color(0.2f, 0.7f, 0.4f, 0.5f));
			Assert.That(g.r, Is.EqualTo(g.g).Within(Tol));
			Assert.That(g.g, Is.EqualTo(g.b).Within(Tol));
			Assert.That(g.a, Is.EqualTo(0.5f).Within(Tol));
		}

		[Test]
		public void RgbHsvRoundTrips()
		{
			var original = new Color(0.3f, 0.6f, 0.9f, 1f);
			Vector3 hsv = ColorMath.RgbToHsv(original);
			Color back = ColorMath.HsvToRgb(hsv.x, hsv.y, hsv.z);
			AssertColor(back, original.r, original.g, original.b, 1f);
		}

		// ---- compiler integration -------------------------------------------------

		private static CompiledExpressionsRegistry NewRegistry() =>
			new(BuiltInTypeRegistry.Default, new ExpressionMethodCompiler());

		private static ExpressionInfo Expr(string id, string returnType, string body,
			IReadOnlyList<(string type, string name)>? args = null) =>
			new(id, args ?? Array.Empty<(string, string)>(), returnType,
				Array.Empty<string>(), Array.Empty<string>(), body);

		[Test]
		public void ExpressionCanCallHsvToRgbWithIntLiterals()
		{
			var registry = NewRegistry();
			registry.CompileAndRegisterAll(new[]
			{
				// All int literals must coerce to the float h/s/v params.
				Expr("hud colour", "colour", "return HsvToRgb(0, 1, 1);"),
			});

			var func = (Func<Color>)registry.GetCompiled("hud colour").@delegate;
			// HSV (0,1,1) is pure red.
			AssertColor(func(), 1f, 0f, 0f, 1f);
		}
	}
}
